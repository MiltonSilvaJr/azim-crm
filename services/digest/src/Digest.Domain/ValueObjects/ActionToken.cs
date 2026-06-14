using System.Security.Cryptography;

namespace Digest.Domain.ValueObjects;

/// <summary>
/// Par (token em claro, hash SHA-256) para tokens de ação de um clique.
/// O token em claro (<see cref="ClearToken"/>) existe apenas em memória durante a emissão;
/// somente o <see cref="TokenHash"/> é persistido (DD-007, RNF 7, ADR-0006).
/// Igualdade por hash.
/// </summary>
public sealed record ActionToken
{
    /// <summary>Token em claro — Base64Url de 32 bytes aleatórios (256 bits de entropia).</summary>
    /// <remarks>Nunca deve ser logado, persistido ou incluído em telemetria.</remarks>
    public string ClearToken { get; }

    /// <summary>Hash SHA-256 do token em claro (32 bytes). Único valor persistido.</summary>
    public byte[] TokenHash { get; }

    private ActionToken(string clearToken, byte[] tokenHash)
    {
        ClearToken = clearToken;
        TokenHash = tokenHash;
    }

    /// <summary>
    /// Emite um novo token com 256 bits de entropia via CSPRNG.
    /// </summary>
    public static ActionToken Issue()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        var clearToken = Base64UrlEncode(rawBytes);
        var hash = ComputeHash(clearToken);
        return new ActionToken(clearToken, hash);
    }

    /// <summary>
    /// Constrói um <see cref="ActionToken"/> a partir de um token em claro conhecido
    /// (usado em verificação ou testes).
    /// </summary>
    public static ActionToken FromClearToken(string clearToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clearToken);
        var hash = ComputeHash(clearToken);
        return new ActionToken(clearToken, hash);
    }

    // ------------------------------------------------------------------
    // Igualdade por hash (dois tokens com o mesmo valor em claro são iguais)
    // ------------------------------------------------------------------

    /// <inheritdoc/>
    public bool Equals(ActionToken? other)
    {
        if (other is null) return false;
        return CryptographicOperations.FixedTimeEquals(TokenHash, other.TokenHash);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var b in TokenHash)
            hash.Add(b);
        return hash.ToHashCode();
    }

    // ------------------------------------------------------------------
    // Helpers privados
    // ------------------------------------------------------------------

    private static byte[] ComputeHash(string clearToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(clearToken);
        return SHA256.HashData(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
