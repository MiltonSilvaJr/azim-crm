using PartnerManagement.Domain.Partners.Events;
using PartnerManagement.Domain.Partners.Exceptions;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Domain.Partners;

/// <summary>
/// Aggregate Root do módulo partner-management.
/// Encapsula as invariantes I1..I5 e acumula domain events para despacho após commit via Outbox.
/// Handlers orquestram — regras de negócio vivem aqui (design §4.1, Princípio P2).
/// <br/>
/// Invariantes protegidas:
/// <list type="bullet">
///   <item>I1: <c>name</c> não vazio após trim (Req 1.1).</item>
///   <item>I2: <c>role</c> pertence à lista canônica vigente do tenant (Req 5.2/5.3).</item>
///   <item>I3: percentuais em [0,00; 100,00] com 2 casas (Req 6.3).</item>
///   <item>I4: parceiro nasce com <c>status = Active</c> (Req 1.5).</item>
///   <item>I5: <c>contact</c>, quando presente, tem e-mail em formato válido (Req 7.2).</item>
/// </list>
/// Mapeia: Req 1, Req 2, Req 3, Req 5, Req 6, Req 7, design §4.1, design §4.5.
/// </summary>
public sealed class Partner
{
    private readonly List<IDomainEvent> _domainEvents = [];

    // =========================================================================
    // Identidade e pertencimento
    // =========================================================================

    /// <summary>Identificador único do parceiro (UUID).</summary>
    public Guid Id { get; private init; }

    /// <summary>Identificador do tenant ao qual o parceiro pertence (RNF 1, DD-001).</summary>
    public Guid TenantId { get; private init; }

    // =========================================================================
    // Atributos de domínio
    // =========================================================================

    /// <summary>Nome do parceiro (possível PII — DD-008).</summary>
    public PartnerName Name { get; private set; } = null!;

    /// <summary>Papel tipado canônico do parceiro.</summary>
    public PartnerRole Role { get; private set; } = null!;

    /// <summary>Percentuais padrão de comissão (pct_setup / pct_recorrente).</summary>
    public CommissionDefaults CommissionDefaults { get; private set; } = null!;

    /// <summary>Dados de contato opcionais do parceiro (possível PII — DD-008).</summary>
    public PartnerContact? Contact { get; private set; }

    /// <summary>Observações livres sobre o parceiro.</summary>
    public string? Notes { get; private set; }

    // =========================================================================
    // State machine (design §4.5)
    // =========================================================================

    /// <summary>Status do ciclo de vida do parceiro.</summary>
    public PartnerStatus Status { get; private set; }

    // =========================================================================
    // Auditoria temporal
    // =========================================================================

    /// <summary>Momento de criação do parceiro (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Autor da criação.</summary>
    public Guid CreatedBy { get; private init; }

    /// <summary>Momento da última atualização (UTC).</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>Autor da última atualização.</summary>
    public Guid? UpdatedBy { get; private set; }

    // =========================================================================
    // Domain events
    // =========================================================================

    /// <summary>
    /// Eventos de domínio acumulados nesta transação.
    /// Despachados após commit via Outbox (design §6.6).
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Remove todos os eventos acumulados (após coleta pelo <c>TransactionBehavior</c>).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    // =========================================================================
    // Construtor privado (uso exclusivo das factories)
    // =========================================================================

    private Partner()
    {
    }

    // =========================================================================
    // Factory — Create
    // =========================================================================

    /// <summary>
    /// Cria um novo parceiro validando todas as invariantes (I1..I5).
    /// Acumula o evento <see cref="PartnerCreated"/>.
    /// </summary>
    /// <param name="tenantId">Tenant proprietário.</param>
    /// <param name="name">Nome do parceiro.</param>
    /// <param name="role">Papel tipado.</param>
    /// <param name="commissionDefaults">Percentuais padrão de comissão.</param>
    /// <param name="contact">Dados de contato opcionais.</param>
    /// <param name="notes">Observações opcionais.</param>
    /// <param name="roleProvider">Provedor da lista canônica de papéis do tenant.</param>
    /// <param name="createdBy">Autor da criação.</param>
    /// <returns>Nova instância de <see cref="Partner"/>.</returns>
    public static Partner Create(
        Guid tenantId,
        string name,
        string role,
        CommissionDefaults commissionDefaults,
        PartnerContact? contact,
        string? notes,
        ICanonicalRoleProvider roleProvider,
        Guid createdBy)
    {
        ArgumentNullException.ThrowIfNull(commissionDefaults);
        ArgumentNullException.ThrowIfNull(roleProvider);

        // I1 — nome não vazio
        PartnerName partnerName = PartnerName.Create(name);

        // I2 — papel canônico
        PartnerRole partnerRole = CreateAndValidateRole(role, tenantId, roleProvider);

        // I3 — percentuais: já validados pelo CommissionDefaults (Percentage.Create lança se inválido)

        // I5 — contato: PartnerContact.Create já valida o e-mail ao construir Email
        // (a exceção sobe diretamente se o e-mail for inválido)

        Partner partner = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = partnerName,
            Role = partnerRole,
            CommissionDefaults = commissionDefaults,
            Contact = contact,
            Notes = notes,
            Status = PartnerStatus.Active, // I4
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };

        partner._domainEvents.Add(new PartnerCreated(
            PartnerId: partner.Id,
            TenantId: tenantId,
            PartnerType: role,
            OccurredAt: partner.CreatedAt));

        return partner;
    }

    // =========================================================================
    // Factory — Reconstitute (uso pelo repositório para rehidratação)
    // =========================================================================

    /// <summary>
    /// Reconstitui um parceiro a partir de dados persistidos, sem revalidar invariantes
    /// e sem acumular eventos (rehidratação do repositório).
    /// </summary>
    public static Partner Reconstitute(
        Guid id,
        Guid tenantId,
        PartnerName name,
        PartnerRole role,
        CommissionDefaults commissionDefaults,
        PartnerContact? contact,
        string? notes,
        PartnerStatus status,
        DateTimeOffset createdAt,
        Guid createdBy,
        DateTimeOffset? updatedAt,
        Guid? updatedBy)
    {
        return new Partner
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Role = role,
            CommissionDefaults = commissionDefaults,
            Contact = contact,
            Notes = notes,
            Status = status,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy
        };
    }

    // =========================================================================
    // Comportamentos
    // =========================================================================

    /// <summary>
    /// Atualiza o perfil do parceiro reaplicando as invariantes I1..I3.
    /// Acumula <see cref="PartnerCommissionPercentagesUpdated"/> quando os percentuais mudam.
    /// </summary>
    public void UpdateProfile(
        string name,
        string role,
        CommissionDefaults commissionDefaults,
        string? notes,
        ICanonicalRoleProvider roleProvider,
        Guid updatedBy)
    {
        ArgumentNullException.ThrowIfNull(commissionDefaults);
        ArgumentNullException.ThrowIfNull(roleProvider);

        // I1
        PartnerName newName = PartnerName.Create(name);

        // I2
        PartnerRole newRole = CreateAndValidateRole(role, TenantId, roleProvider);

        // I3: percentuais já validados em CommissionDefaults.Create / Percentage.Create

        bool percentualsChanged =
            CommissionDefaults.PctSetup != commissionDefaults.PctSetup ||
            CommissionDefaults.PctRecorrente != commissionDefaults.PctRecorrente;

        Name = newName;
        Role = newRole;
        CommissionDefaults = commissionDefaults;
        Notes = notes;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;

        if (percentualsChanged)
        {
            _domainEvents.Add(new PartnerCommissionPercentagesUpdated(
                PartnerId: Id,
                TenantId: TenantId,
                ChangedFields: ["pct_setup", "pct_recorrente"],
                OccurredAt: UpdatedAt.Value));
        }
    }

    /// <summary>
    /// Atualiza os dados de contato do parceiro.
    /// </summary>
    public void UpdateContact(PartnerContact? contact)
    {
        Contact = contact;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Inativa o parceiro (soft-delete lógico via <c>active = false</c>).
    /// Idempotente: se já inativo, não altera estado nem emite evento de transição (DD-006, PBT-02).
    /// Registra evento apenas em transição efetiva.
    /// </summary>
    public bool Deactivate()
    {
        if (Status == PartnerStatus.Inactive)
        {
            return false; // sem transição efetiva
        }

        Status = PartnerStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new PartnerDeactivated(
            PartnerId: Id,
            TenantId: TenantId,
            OccurredAt: UpdatedAt.Value));

        return true;
    }

    /// <summary>
    /// Reativa o parceiro.
    /// Idempotente: se já ativo, não altera estado nem emite evento de transição (DD-006, PBT-02).
    /// Registra evento apenas em transição efetiva.
    /// </summary>
    public bool Reactivate()
    {
        if (Status == PartnerStatus.Active)
        {
            return false; // sem transição efetiva
        }

        Status = PartnerStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new PartnerReactivated(
            PartnerId: Id,
            TenantId: TenantId,
            OccurredAt: UpdatedAt.Value));

        return true;
    }

    // =========================================================================
    // Guards privados
    // =========================================================================

    private static PartnerRole CreateAndValidateRole(string role, Guid tenantId, ICanonicalRoleProvider roleProvider)
    {
        // Primeiro valida não-vazio (PartnerRole.Create) e depois valida canonicidade (I2)
        PartnerRole partnerRole = PartnerRole.Create(role);

        if (!roleProvider.IsCanonical(partnerRole.Value, tenantId))
        {
            throw new InvalidPartnerRoleException(role);
        }

        return partnerRole;
    }
}
