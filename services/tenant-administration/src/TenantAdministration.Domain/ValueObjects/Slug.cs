using System.Text.RegularExpressions;
using TenantAdministration.Domain.Common;
using TenantAdministration.Domain.Errors;

namespace TenantAdministration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o identificador textual único e imutável do tenant.
/// Regras: apenas [a-z-], comprimento 3..40, sem hífen nas bordas, sem hífens consecutivos.
/// PBT-01 (imutabilidade) e PBT-02 (formato) validados em Domain.Tests.
/// </summary>
public sealed class Slug : IEquatable<Slug>
{
    /// <summary>Comprimento mínimo do slug.</summary>
    public const int MinLength = 3;

    /// <summary>Comprimento máximo do slug.</summary>
    public const int MaxLength = 40;

    private static readonly Regex ValidPattern =
        new(@"^[a-z][a-z-]{1,38}[a-z]$|^[a-z]{3,40}$", RegexOptions.Compiled);

    // Padrão geral: apenas letras e hífens
    private static readonly Regex AllowedChars =
        new(@"^[a-z-]+$", RegexOptions.Compiled);

    /// <summary>Valor normalizado do slug.</summary>
    public string Value { get; }

    private Slug(string value) => Value = value;

    /// <summary>
    /// Cria um <see cref="Slug"/> a partir de uma string bruta.
    /// Aplica trim e lowercase antes da validação; rejeita qualquer outro caractere fora de [a-z-].
    /// </summary>
    /// <param name="raw">String bruta de entrada.</param>
    /// <returns>Sucesso com o slug ou falha com TA-ERR-005.</returns>
    public static Result<Slug> Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Fail();

        var normalized = raw.Trim().ToLowerInvariant();

        if (normalized.Length < MinLength || normalized.Length > MaxLength)
            return Fail();

        if (!AllowedChars.IsMatch(normalized))
            return Fail();

        if (normalized.StartsWith('-') || normalized.EndsWith('-'))
            return Fail();

        if (normalized.Contains("--"))
            return Fail();

        return Result<Slug>.Success(new Slug(normalized));
    }

    private static Result<Slug> Fail()
    {
        var (code, message) = DomainErrors.SlugInvalidFormat;
        return Result<Slug>.Failure(code, message);
    }

    /// <inheritdoc/>
    public bool Equals(Slug? other) => other is not null && Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Slug other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Igualdade estrutural por valor.</summary>
    public static bool operator ==(Slug? left, Slug? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Desigualdade estrutural por valor.</summary>
    public static bool operator !=(Slug? left, Slug? right) => !(left == right);
}
