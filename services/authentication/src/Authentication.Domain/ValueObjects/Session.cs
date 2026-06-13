namespace Authentication.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o estado e a validade de um token de sessão.
///
/// Produzido pelo <c>SessionTokenValidator</c> a cada requisição.
/// Imutável após construção; a "transição de estado" produz uma nova instância.
///
/// Invariantes (design.md § 4.3, § 4.5):
///   - <c>tenant_id</c> obrigatório e não-vazio.
///   - <c>expires_at</c> deve ser posterior a <c>issued_at</c>.
///   - <c>state</c> coerente com <c>expires_at</c>: se <c>now >= expires_at</c>, sessão não é acessível.
///   - Estados <c>Expired</c> e <c>Revoked</c> são terminais: <see cref="IsAccessible"/> sempre retorna false.
///
/// Mapeia: Req 4.2, Req 9.4, design.md § 4.3, § 4.5.
/// </summary>
public sealed class Session : IEquatable<Session>
{
    /// <summary>Estado atual da sessão.</summary>
    public SessionState State { get; }

    /// <summary>Identificador do tenant ao qual esta sessão está escopada.</summary>
    public Guid TenantId { get; }

    /// <summary>Momento UTC em que o token foi emitido pelo IdP.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>Momento UTC em que o token expira (TTL do Identity Platform — DD-008).</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Indica se a checagem de revogação foi realizada nesta validação.
    /// Quando false, o validador deve verificar o status de revogação no IdP.
    /// </summary>
    public bool RevocationChecked { get; }

    private Session(
        SessionState state,
        Guid tenantId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        bool revocationChecked)
    {
        State = state;
        TenantId = tenantId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        RevocationChecked = revocationChecked;
    }

    /// <summary>
    /// Cria uma instância de <see cref="Session"/> com validação de invariantes.
    /// </summary>
    /// <param name="state">Estado inicial da sessão.</param>
    /// <param name="tenantId">UUID do tenant (não pode ser <see cref="Guid.Empty"/>).</param>
    /// <param name="issuedAt">Momento UTC de emissão do token.</param>
    /// <param name="expiresAt">Momento UTC de expiração do token.</param>
    /// <param name="revocationChecked">Indica se revogação foi verificada.</param>
    /// <returns>Nova instância imutável de <see cref="Session"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Lançada quando <paramref name="tenantId"/> é vazio ou
    /// <paramref name="expiresAt"/> é anterior a <paramref name="issuedAt"/>.
    /// </exception>
    public static Session Create(
        SessionState state,
        Guid tenantId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        bool revocationChecked)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser Guid.Empty.", nameof(tenantId));

        if (expiresAt < issuedAt)
            throw new ArgumentException(
                "expiresAt não pode ser anterior a issuedAt.",
                nameof(expiresAt));

        return new Session(state, tenantId, issuedAt, expiresAt, revocationChecked);
    }

    /// <summary>
    /// Determina se esta sessão concede acesso a recursos protegidos no momento <paramref name="now"/>.
    ///
    /// Retorna <see langword="true"/> somente quando:
    ///   - Estado é <see cref="SessionState.Authenticated"/>
    ///   - O token não expirou (<paramref name="now"/> &lt; <see cref="ExpiresAt"/>)
    ///
    /// Estados <see cref="SessionState.Expired"/>, <see cref="SessionState.Revoked"/> e
    /// <see cref="SessionState.Anonymous"/> são sempre inacessíveis, independente de <paramref name="now"/>.
    ///
    /// Mapeia: design.md § 4.5, Req 9.4.
    /// </summary>
    /// <param name="now">Momento UTC corrente para avaliação de expiração.</param>
    /// <returns><see langword="true"/> quando o acesso deve ser concedido.</returns>
    public bool IsAccessible(DateTimeOffset now) =>
        State == SessionState.Authenticated && now < ExpiresAt;

    /// <inheritdoc/>
    public bool Equals(Session? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return State == other.State &&
               TenantId == other.TenantId &&
               IssuedAt == other.IssuedAt &&
               ExpiresAt == other.ExpiresAt &&
               RevocationChecked == other.RevocationChecked;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Session other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(State, TenantId, IssuedAt, ExpiresAt, RevocationChecked);

    /// <summary>Compara duas sessões por valor.</summary>
    public static bool operator ==(Session? left, Session? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compara duas sessões por valor (desigualdade).</summary>
    public static bool operator !=(Session? left, Session? right) => !(left == right);
}
