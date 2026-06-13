using FluentAssertions;
using TenantAdministration.Domain.Policies;
using Xunit;

namespace TenantAdministration.Domain.Tests.Policies;

/// <summary>
/// Testes para AssetValidationPolicy.
/// Cobre TASK-04: validação de formato, tamanho e sanitização SVG.
/// </summary>
public sealed class AssetValidationPolicyTests
{
    // Magic bytes de referência
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] IcoMagic = [0x00, 0x00, 0x01, 0x00];

    private static Stream MakeStream(byte[] magic, int totalSize = 100)
    {
        var data = new byte[totalSize];
        Array.Copy(magic, data, Math.Min(magic.Length, data.Length));
        return new MemoryStream(data);
    }

    private static Stream MakeSvgStream(string svgContent)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(svgContent);
        return new MemoryStream(bytes);
    }

    // ──────────────────────────────────────────────
    // PNG válido
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_ValidPng_ReturnsSuccess()
    {
        using var stream = MakeStream(PngMagic, 1000);
        var result = AssetValidationPolicy.Validate(stream, "image/png", 1000);
        result.IsSuccess.Should().BeTrue(because: "PNG válido deve ser aceito");
    }

    // ──────────────────────────────────────────────
    // SVG válido
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_ValidSvg_ReturnsSuccess()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg'><circle r='10'/></svg>";
        using var stream = MakeSvgStream(svg);
        var result = AssetValidationPolicy.Validate(stream, "image/svg+xml", stream.Length);
        result.IsSuccess.Should().BeTrue(because: "SVG válido deve ser aceito");
    }

    // ──────────────────────────────────────────────
    // JPEG deve ser rejeitado
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_JpegFile_ReturnsFailureWithTaErr013()
    {
        using var stream = MakeStream(JpegMagic, 500);
        var result = AssetValidationPolicy.Validate(stream, "image/jpeg", 500);
        result.IsFailure.Should().BeTrue(because: "JPEG não é suportado para logo");
        result.ErrorCode.Should().Be("TA-ERR-013");
    }

    // ──────────────────────────────────────────────
    // Arquivo > 1 MB
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_FileExceeds1MB_ReturnsFailureWithTaErr012()
    {
        using var stream = MakeStream(PngMagic, 100);
        var sizeBytes = AssetValidationPolicy.MaxSizeBytesLogo + 1;
        var result = AssetValidationPolicy.Validate(stream, "image/png", sizeBytes);
        result.IsFailure.Should().BeTrue(because: "arquivo maior que 1 MB deve ser rejeitado");
        result.ErrorCode.Should().Be("TA-ERR-012");
    }

    [Fact]
    public void Validate_FileExactly1MB_ReturnsSuccess()
    {
        using var stream = MakeStream(PngMagic, 100);
        var result = AssetValidationPolicy.Validate(stream, "image/png", AssetValidationPolicy.MaxSizeBytesLogo);
        result.IsSuccess.Should().BeTrue(because: "arquivo de exatamente 1 MB deve ser aceito");
    }

    // ──────────────────────────────────────────────
    // ICO aceito para favicon
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_ValidIco_ReturnsSuccess()
    {
        using var stream = MakeStream(IcoMagic, 200);
        var result = AssetValidationPolicy.Validate(stream, "image/x-icon", 200);
        result.IsSuccess.Should().BeTrue(because: "ICO é aceito para favicon");
    }

    // ──────────────────────────────────────────────
    // Sanitização SVG
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_SvgWithScript_ReturnsFailure()
    {
        const string maliciousSvg = "<svg xmlns='http://www.w3.org/2000/svg'><script>alert('xss')</script></svg>";
        using var stream = MakeSvgStream(maliciousSvg);
        var result = AssetValidationPolicy.Validate(stream, "image/svg+xml", stream.Length);
        result.IsFailure.Should().BeTrue(because: "SVG com <script> deve ser rejeitado");
        result.ErrorCode.Should().Be("TA-ERR-013");
    }

    [Fact]
    public void Validate_SvgWithOnClickHandler_ReturnsFailure()
    {
        const string maliciousSvg = "<svg xmlns='http://www.w3.org/2000/svg'><circle onclick='xss()' r='10'/></svg>";
        using var stream = MakeSvgStream(maliciousSvg);
        var result = AssetValidationPolicy.Validate(stream, "image/svg+xml", stream.Length);
        result.IsFailure.Should().BeTrue(because: "SVG com handler on* deve ser rejeitado");
        result.ErrorCode.Should().Be("TA-ERR-013");
    }

    [Fact]
    public void Validate_SvgWithExternalXlinkHref_ReturnsFailure()
    {
        const string maliciousSvg = "<svg xmlns='http://www.w3.org/2000/svg'><image xlink:href='http://evil.com/payload'/></svg>";
        using var stream = MakeSvgStream(maliciousSvg);
        var result = AssetValidationPolicy.Validate(stream, "image/svg+xml", stream.Length);
        result.IsFailure.Should().BeTrue(because: "SVG com xlink:href externo deve ser rejeitado");
        result.ErrorCode.Should().Be("TA-ERR-013");
    }

    // ──────────────────────────────────────────────
    // Magic bytes verificados (não só extensão)
    // ──────────────────────────────────────────────

    [Fact]
    public void Validate_JpegDisguisedAsPng_ReturnsFailure()
    {
        // Magic bytes de JPEG, media type declarado como PNG — deve rejeitar pelo magic bytes real
        using var stream = MakeStream(JpegMagic, 500);
        var result = AssetValidationPolicy.Validate(stream, "image/png", 500);
        result.IsFailure.Should().BeTrue(
            because: "conteúdo JPEG disfarçado como PNG deve ser rejeitado pelos magic bytes");
    }
}
