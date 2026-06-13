using System.Text.RegularExpressions;
using TenantAdministration.Domain.Common;
using TenantAdministration.Domain.Errors;

namespace TenantAdministration.Domain.Policies;

/// <summary>
/// Valida formato real (magic bytes), tamanho e segurança de assets de branding.
/// Formatos aceitos para logo: PNG (<c>image/png</c>) e SVG (<c>image/svg+xml</c>).
/// Formatos aceitos para favicon: ICO (<c>image/x-icon</c>), PNG e SVG.
/// Tamanho máximo: 1 MB (1.048.576 bytes).
/// SVG é sanitizado: <c>&lt;script&gt;</c>, handlers <c>on*</c> e <c>xlink:href</c>
/// externos causam rejeição (DD-007).
/// </summary>
public static class AssetValidationPolicy
{
    /// <summary>Limite máximo de tamanho de arquivo: 1 MB.</summary>
    public const long MaxSizeBytesLogo = 1_048_576L;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] IcoSignature = [0x00, 0x00, 0x01, 0x00];

    // Padrões de detecção de conteúdo malicioso em SVG
    private static readonly Regex ScriptTagPattern =
        new(@"<\s*script[\s>]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EventHandlerPattern =
        new(@"\bon\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExternalXlinkPattern =
        new(@"xlink:href\s*=\s*[""']https?://", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Valida o asset: verifica tamanho, magic bytes e segurança (SVG).
    /// </summary>
    /// <param name="content">Stream do conteúdo do arquivo.</param>
    /// <param name="mediaType">Tipo MIME declarado pelo cliente.</param>
    /// <param name="sizeBytes">Tamanho em bytes (informado pelo caller, verificado antes do upload).</param>
    /// <returns>Sucesso ou falha com TA-ERR-012/013.</returns>
    public static Result<bool> Validate(Stream content, string mediaType, long sizeBytes)
    {
        // 1. Verificar tamanho antes de qualquer processamento
        if (sizeBytes > MaxSizeBytesLogo)
        {
            var (code, message) = DomainErrors.AssetTooLarge;
            return Result<bool>.Failure(code, message);
        }

        // 2. Ler magic bytes para verificação real do formato
        var header = ReadHeader(content, 8);

        // 3. Determinar tipo real pelo conteúdo (não pela extensão/media type declarado)
        if (IsPng(header))
        {
            return Result<bool>.Success(true);
        }

        if (IsIco(header))
        {
            return Result<bool>.Success(true);
        }

        if (IsSvg(content))
        {
            // 4. Sanitização SVG: rejeitar conteúdo perigoso (DD-007)
            return ValidateSvgContent(content);
        }

        // Formato não reconhecido ou não suportado
        var (errCode, errMessage) = DomainErrors.AssetUnsupportedFormat;
        return Result<bool>.Failure(errCode, errMessage);
    }

    private static byte[] ReadHeader(Stream stream, int count)
    {
        stream.Position = 0;
        var buffer = new byte[count];
        var read = stream.Read(buffer, 0, count);
        stream.Position = 0;
        return buffer[..read];
    }

    private static bool IsPng(byte[] header) =>
        header.Length >= PngSignature.Length &&
        header[..PngSignature.Length].SequenceEqual(PngSignature);

    private static bool IsIco(byte[] header) =>
        header.Length >= IcoSignature.Length &&
        header[..IcoSignature.Length].SequenceEqual(IcoSignature);

    /// <summary>
    /// Verifica se o conteúdo do stream é um SVG (começa com XML/SVG tag).
    /// SVG é texto; o stream deve ser lido como UTF-8.
    /// </summary>
    private static bool IsSvg(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        // Lê os primeiros 200 chars para verificar se é SVG
        var start = reader.ReadToEnd();
        stream.Position = 0;

        // SVG pode começar com BOM, declaração XML ou diretamente com <svg
        return start.TrimStart().StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
               start.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static Result<bool> ValidateSvgContent(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        var content = reader.ReadToEnd();
        stream.Position = 0;

        if (ScriptTagPattern.IsMatch(content) ||
            EventHandlerPattern.IsMatch(content) ||
            ExternalXlinkPattern.IsMatch(content))
        {
            var (code, message) = DomainErrors.AssetUnsupportedFormat;
            return Result<bool>.Failure(code, message);
        }

        return Result<bool>.Success(true);
    }
}
