using Organization.Domain.Events;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

/// <summary>
/// Aggregate root de User.
/// Encapsula <see cref="UserMembership"/> (vínculo usuário-BU-papel).
///
/// Invariantes protegidas:
/// - E-mail único por tenant (via índice).
/// - No máximo um membership por BU (<c>MembershipUniquenessSpec</c>).
/// - Papel pertence à lista canônica.
/// - Soft-delete preserva histórico (memberships não são removidos fisicamente).
/// - Não chama repositório ou serviço externo (regras puras no domínio).
/// </summary>
public sealed class User : AggregateRoot
{
    private readonly List<UserMembership> _memberships = [];

    /// <summary>Identificador único do usuário.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador do tenant.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>E-mail do usuário (PII — não expor em logs nem em eventos).</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Nome de exibição (PII — não expor em logs nem em eventos).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>UID da identidade no GCP Identity Platform.</summary>
    public string IdentityUid { get; private set; } = string.Empty;

    /// <summary>Indica se o usuário está ativo.</summary>
    public bool Active { get; private set; }

    /// <summary>Momento de criação.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Momento de desativação (preenchido quando <see cref="Active"/> = false).</summary>
    public DateTimeOffset? DeactivatedAt { get; private set; }

    /// <summary>Memberships do usuário (somente leitura). Preservados mesmo após desativação.</summary>
    public IReadOnlyList<UserMembership> Memberships => _memberships.AsReadOnly();

    private User() { }

    /// <summary>
    /// Ativa (cria) um usuário a partir do aceite de convite ou provisionamento inicial.
    /// Emite <see cref="UserActivated"/>.
    /// </summary>
    /// <param name="email">E-mail do usuário (PII).</param>
    /// <param name="displayName">Nome de exibição (PII).</param>
    /// <param name="identityUid">UID do GCP Identity Platform.</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="now">Instante de ativação (fornecido pela Application).</param>
    public static User Activate(
        string email,
        string displayName,
        string identityUid,
        Guid tenantId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("O e-mail do usuário não pode ser nulo ou vazio.", nameof(email));

        if (string.IsNullOrWhiteSpace(identityUid))
            throw new ArgumentException("O UID de identidade não pode ser nulo ou vazio.", nameof(identityUid));

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            DisplayName = displayName ?? string.Empty,
            IdentityUid = identityUid,
            Active = true,
            CreatedAt = now,
        };

        user.RaiseDomainEvent(new UserActivated(tenantId, user.Id, [], now));
        return user;
    }

    /// <summary>
    /// Atribui um membership (vínculo BU-papel) ao usuário.
    /// Emite <see cref="MembershipRoleChanged"/> (atribuição inicial tratada como role change pelo design).
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="role">Papel a atribuir.</param>
    /// <param name="membershipId">Identificador pré-gerado do membership.</param>
    /// <exception cref="DomainException">
    ///   ORG-ERR-012 quando já existe membership para esta BU.
    ///   Quando usuário está inativo.
    /// </exception>
    public void AssignMembership(Guid buId, Role role, Guid membershipId)
    {
        EnsureActive();

        if (_memberships.Any(m => m.BuId == buId))
            throw new DomainException(
                "ORG-ERR-012",
                $"O usuário já possui vínculo na BU '{buId}'. ORG-ERR-012");

        _memberships.Add(new UserMembership(membershipId, buId, role));
    }

    /// <summary>
    /// Altera o papel de um membership existente.
    /// Emite <see cref="MembershipRoleChanged"/>.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <param name="newRole">Novo papel.</param>
    /// <exception cref="DomainException">Quando a BU não tem membership ou usuário inativo.</exception>
    public void ChangeMembershipRole(Guid buId, Role newRole)
    {
        EnsureActive();

        var membership = _memberships.FirstOrDefault(m => m.BuId == buId)
            ?? throw new DomainException(
                "ORG-ERR-016",
                $"Não existe membership para a BU '{buId}' neste usuário.");

        membership.ChangeRole(newRole);
        RaiseDomainEvent(new MembershipRoleChanged(TenantId, Id, buId, newRole.Value, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Remove o membership de uma BU.
    /// </summary>
    /// <param name="buId">Identificador da BU.</param>
    /// <exception cref="DomainException">Quando a BU não tem membership.</exception>
    public void RemoveMembership(Guid buId)
    {
        var membership = _memberships.FirstOrDefault(m => m.BuId == buId)
            ?? throw new DomainException(
                "ORG-ERR-016",
                $"Não existe membership para a BU '{buId}' neste usuário.");

        _memberships.Remove(membership);
    }

    /// <summary>
    /// Desativa o usuário (soft-delete). Preserva histórico de memberships.
    /// Emite <see cref="UserDeactivated"/>.
    /// </summary>
    /// <param name="now">Instante de desativação.</param>
    /// <exception cref="DomainException">Quando o usuário já está desativado.</exception>
    public void Deactivate(DateTimeOffset now)
    {
        if (!Active)
            throw new DomainException("ORG-ERR-002", "O usuário já está desativado.");

        Active = false;
        DeactivatedAt = now;
        RaiseDomainEvent(new UserDeactivated(TenantId, Id, now));
    }

    // ── Invariantes privadas ──────────────────────────────────────────────────

    private void EnsureActive()
    {
        if (!Active)
            throw new DomainException("ORG-ERR-002", "Não é possível operar em um usuário inativo.");
    }
}
