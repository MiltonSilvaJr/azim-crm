namespace Authentication.Domain.ValueObjects;

/// <summary>
/// Objeto de valor central do módulo authentication.
///
/// Representa a identidade interna do usuário escopada a exatamente um tenant Azim.
/// Produzido pelo <c>AuthContextComposer</c> a partir de um <c>IdentityRef</c> (ACL)
/// e injetado na requisição após validação bem-sucedida.
///
/// Invariantes (design.md § 4.3, Req 5.2, 5.3, PBT-01):
///   - <c>user_id</c> e <c>tenant_id</c> obrigatórios e não-vazios.
///   - <c>email</c> obrigatório e não-vazio.
///   - Exatamente um <c>tenant_id</c> (campo escalar).
///   - Nunca contém <c>identity_uid</c> nem qualquer claim do Identity Provider (DD-001).
///   - Imutável após composição.
/// </summary>
public sealed class AuthContext : IEquatable<AuthContext>
{
    /// <summary>Identificador interno do usuário no tenant (UUID do módulo organization).</summary>
    public Guid UserId { get; }

    /// <summary>Identificador único do tenant ao qual esta sessão está escopada (PBT-01).</summary>
    public Guid TenantId { get; }

    /// <summary>E-mail do usuário no tenant.</summary>
    public string Email { get; }

    /// <summary>Papéis globais do usuário no tenant.</summary>
    public IReadOnlyList<string> Roles { get; }

    /// <summary>Memberships do usuário (unidade de negócio → papel).</summary>
    public MembershipSet Memberships { get; }

    private AuthContext(
        Guid userId,
        Guid tenantId,
        string email,
        IReadOnlyList<string> roles,
        MembershipSet memberships)
    {
        UserId = userId;
        TenantId = tenantId;
        Email = email;
        Roles = roles;
        Memberships = memberships;
    }

    /// <summary>
    /// Cria um <see cref="AuthContext"/> com validação de todas as invariantes.
    /// </summary>
    /// <param name="userId">UUID do usuário (não pode ser <see cref="Guid.Empty"/>).</param>
    /// <param name="tenantId">UUID do tenant (não pode ser <see cref="Guid.Empty"/>).</param>
    /// <param name="email">E-mail do usuário (não nulo nem vazio).</param>
    /// <param name="roles">Lista de papéis globais (pode ser vazia).</param>
    /// <param name="memberships">Conjunto de memberships por unidade de negócio.</param>
    /// <returns>Instância imutável de <see cref="AuthContext"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Lançada quando <paramref name="userId"/> ou <paramref name="tenantId"/> são <see cref="Guid.Empty"/>,
    /// ou quando <paramref name="email"/> é nulo ou vazio.
    /// </exception>
    public static AuthContext Create(
        Guid userId,
        Guid tenantId,
        string email,
        IEnumerable<string> roles,
        MembershipSet memberships)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("user_id não pode ser Guid.Empty.", nameof(userId));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser Guid.Empty.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("email é obrigatório e não pode ser vazio.", nameof(email));

        return new AuthContext(
            userId,
            tenantId,
            email,
            roles.ToList().AsReadOnly(),
            memberships);
    }

    /// <inheritdoc/>
    public bool Equals(AuthContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return UserId == other.UserId &&
               TenantId == other.TenantId &&
               string.Equals(Email, other.Email, StringComparison.Ordinal) &&
               Roles.SequenceEqual(other.Roles) &&
               Memberships.Equals(other.Memberships);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AuthContext other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(UserId, TenantId, Email);

    /// <summary>Compara dois contextos por valor.</summary>
    public static bool operator ==(AuthContext? left, AuthContext? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compara dois contextos por valor (desigualdade).</summary>
    public static bool operator !=(AuthContext? left, AuthContext? right) => !(left == right);
}
