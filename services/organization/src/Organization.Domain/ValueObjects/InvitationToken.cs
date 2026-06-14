namespace Organization.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o token de convite de usuário.
/// Armazena apenas o hash do token; o valor em claro nunca é persistido (RNF 3, DD-009).
/// O token em claro existe apenas no momento da emissão para envio por e-mail.
/// </summary>
public sealed class InvitationToken : IEquatable<InvitationToken>
{
    /// <summary>Hash do token de convite.</summary>
    public string TokenHash { get; }

    private InvitationToken(string tokenHash)
    {
        TokenHash = tokenHash;
    }

    /// <summary>
    /// Cria um <see cref="InvitationToken"/> a partir de um hash já calculado.
    /// Usado ao recuperar do repositório ou ao validar no aceite.
    /// </summary>
    /// <param name="tokenHash">Hash do token.</param>
    /// <exception cref="ArgumentException">Quando o hash é nulo ou vazio.</exception>
    public static InvitationToken FromHash(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("O hash do token de convite não pode ser nulo ou vazio.", nameof(tokenHash));

        return new InvitationToken(tokenHash);
    }

    /// <summary>
    /// Verifica se o hash fornecido corresponde ao hash armazenado.
    /// A comparação é feita diretamente entre hashes (nunca entre texto claro).
    /// </summary>
    /// <param name="candidateHash">Hash candidato para comparação.</param>
    /// <returns><c>true</c> se os hashes coincidem; <c>false</c> caso contrário.</returns>
    public bool Matches(string candidateHash)
        => string.Equals(TokenHash, candidateHash, StringComparison.Ordinal);

    /// <inheritdoc/>
    public bool Equals(InvitationToken? other)
    {
        if (other is null) return false;
        return string.Equals(TokenHash, other.TokenHash, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is InvitationToken other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => TokenHash.GetHashCode(StringComparison.Ordinal);

    /// <summary>Retorna representação reduzida sem expor o hash completo.</summary>
    public override string ToString() => "[InvitationToken]";

    /// <summary>Operador de igualdade.</summary>
    public static bool operator ==(InvitationToken? left, InvitationToken? right)
        => left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade.</summary>
    public static bool operator !=(InvitationToken? left, InvitationToken? right)
        => !(left == right);
}
