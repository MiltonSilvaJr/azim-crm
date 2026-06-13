using Organization.Domain.Events;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

/// <summary>
/// Aggregate root de convite de usuário.
/// Implementa a máquina de estados: <c>Pending → Accepted | Revoked | Expired</c>.
///
/// Invariantes protegidas:
/// - Apenas transições a partir de <see cref="InvitationState.Pending"/> são válidas (PBT-04).
/// - Estados terminais (Accepted/Revoked/Expired) rejeitam toda tentativa de transição.
/// - O aceite valida o hash do token e a expiração.
/// - Token armazenado apenas como hash (RNF 3, DD-009).
/// - Ao menos uma BU/papel no <c>targetMemberships</c>.
/// </summary>
public sealed class UserInvitation : AggregateRoot
{
    private readonly List<(Guid BuId, Role Role)> _targetMemberships = [];

    /// <summary>Identificador único do convite.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador do tenant.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>E-mail do convidado (PII — não expor em logs nem em eventos).</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Hash do token de convite (nunca o valor em claro).</summary>
    public InvitationToken TokenHash { get; private set; } = null!;

    /// <summary>Estado atual da máquina de estados do convite.</summary>
    public InvitationState State { get; private set; }

    /// <summary>Memberships de destino ao aceitar o convite (BU → papel).</summary>
    public IReadOnlyList<(Guid BuId, Role Role)> TargetMemberships => _targetMemberships.AsReadOnly();

    /// <summary>Momento de expiração do convite.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Momento de criação do convite.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Momento de aceite do convite (preenchido quando <see cref="State"/> = <see cref="InvitationState.Accepted"/>).</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    private UserInvitation() { }

    /// <summary>
    /// Cria um novo convite em estado <see cref="InvitationState.Pending"/>.
    /// Emite <see cref="UserInvited"/>.
    /// </summary>
    /// <param name="email">E-mail do convidado (PII).</param>
    /// <param name="tokenHash">Token de convite (somente hash armazenado).</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="targetMemberships">Memberships a criar no aceite (BU → papel).</param>
    /// <param name="expiresAt">Momento de expiração.</param>
    /// <param name="now">Instante de criação (fornecido pela Application).</param>
    /// <exception cref="ArgumentException">Quando e-mail é nulo ou vazio.</exception>
    /// <exception cref="DomainException">Quando targetMemberships está vazio.</exception>
    public static UserInvitation Create(
        string email,
        InvitationToken tokenHash,
        Guid tenantId,
        IEnumerable<(Guid BuId, Role Role)> targetMemberships,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("O e-mail do convidado não pode ser nulo ou vazio.", nameof(email));

        var memberships = targetMemberships.ToList();
        if (memberships.Count == 0)
            throw new DomainException(
                "ORG-ERR-007",
                "O convite precisa ter ao menos uma BU/papel de destino.");

        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            TokenHash = tokenHash,
            State = InvitationState.Pending,
            ExpiresAt = expiresAt,
            CreatedAt = now,
        };

        invitation._targetMemberships.AddRange(memberships);
        invitation.RaiseDomainEvent(new UserInvited(tenantId, invitation.Id, now));
        return invitation;
    }

    /// <summary>
    /// Aceita o convite após validação do token e expiração.
    /// Transição: <see cref="InvitationState.Pending"/> → <see cref="InvitationState.Accepted"/>.
    /// </summary>
    /// <param name="candidateHash">Hash do token fornecido pelo convidado.</param>
    /// <param name="now">Instante do aceite (fornecido pela Application via IClock).</param>
    /// <exception cref="DomainException">
    ///   ORG-ERR-004 quando token inválido ou convite expirado.
    ///   ORG-ERR-006 quando estado não é <see cref="InvitationState.Pending"/>.
    /// </exception>
    public void Accept(string candidateHash, DateTimeOffset now)
    {
        EnsureTerminalNotReached(
            $"O convite não pode ser aceito pois está no estado '{State}'. ORG-ERR-006");

        // Valida expiração (expiresAt é exclusivo)
        if (now >= ExpiresAt)
            throw new DomainException(
                "ORG-ERR-004",
                "Convite inválido ou expirado. ORG-ERR-004");

        // Valida hash do token
        if (!TokenHash.Matches(candidateHash))
            throw new DomainException(
                "ORG-ERR-004",
                "Convite inválido ou expirado. ORG-ERR-004");

        State = InvitationState.Accepted;
        AcceptedAt = now;
    }

    /// <summary>
    /// Revoga o convite.
    /// Transição: <see cref="InvitationState.Pending"/> → <see cref="InvitationState.Revoked"/>.
    /// </summary>
    /// <param name="now">Instante da revogação.</param>
    /// <exception cref="DomainException">ORG-ERR-006 quando estado não é <see cref="InvitationState.Pending"/>.</exception>
    public void Revoke(DateTimeOffset now)
    {
        EnsureTerminalNotReached(
            $"O convite não pode ser revogado pois está no estado '{State}'. ORG-ERR-006");

        State = InvitationState.Revoked;
    }

    /// <summary>
    /// Materializa a expiração do convite (por job de varredura).
    /// Transição: <see cref="InvitationState.Pending"/> → <see cref="InvitationState.Expired"/>.
    /// </summary>
    /// <param name="now">Instante da expiração.</param>
    /// <exception cref="DomainException">Quando estado não é <see cref="InvitationState.Pending"/>.</exception>
    public void Expire(DateTimeOffset now)
    {
        EnsureTerminalNotReached(
            $"O convite não pode expirar pois está no estado '{State}'.");

        State = InvitationState.Expired;
    }

    // ── Invariantes privadas ──────────────────────────────────────────────────

    private void EnsureTerminalNotReached(string message)
    {
        if (State != InvitationState.Pending)
            throw new DomainException("ORG-ERR-006", message);
    }
}
