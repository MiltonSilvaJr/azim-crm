using AccountManagement.Domain.Accounts.Events;
using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Domain.Shared;

namespace AccountManagement.Domain.Accounts;

/// <summary>
/// Aggregate Root do módulo account-management.
///
/// Invariantes protegidas pelo root (design §4.1):
/// - I1: nome não vazio (<see cref="AccountName"/> válido — Req 1.5).
/// - I2: <see cref="NormalizedName"/> derivado de <see cref="Name"/> via
///   <see cref="NameNormalizer"/> e recalculado a cada alteração (Req 1.1, Req 4.2).
/// - I3: todo <see cref="Contact"/> carrega o mesmo <see cref="TenantId"/> e
///   o <see cref="Id"/> do root (Req 5.1).
/// - I4: <see cref="BuId"/> é obrigatório e imutável após criação (ADR-0009, VAL-ACC-03).
///
/// Contas são segmentadas por Business Unit — <c>bu_id</c> obrigatório (ADR-0009, Req 2.3 revisado).
/// Domain events são acumulados na coleção <see cref="DomainEvents"/> e despachados
/// via Outbox após commit (DD-007).
///
/// Mapeia: design §4.1, Req 1..5, Req 7, PBT-03, ADR-0009.
/// </summary>
public sealed class Account
{
    // =========================================================================
    // Estado
    // =========================================================================

    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<Contact> _contacts = [];

    /// <summary>Identificador único da conta (UUID).</summary>
    public Guid Id { get; private set; }

    /// <summary>Tenant ao qual a conta pertence. Imutável após criação.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Business Unit dona da conta. Imutável após criação (I4 — ADR-0009).
    /// Uma conta pertence a exatamente uma BU dentro de seu tenant.
    /// </summary>
    public Guid BuId { get; private set; }

    /// <summary>Nome da conta (Req 1.5).</summary>
    public AccountName Name { get; private set; }

    /// <summary>
    /// Forma normalizada do nome — base de dedupe e busca indexada (DD-005, DD-006).
    /// Sempre derivada de <see cref="Name"/> via <see cref="NameNormalizer"/> (I2).
    /// </summary>
    public NormalizedName NormalizedName { get; private set; }

    /// <summary>Website da conta (opcional).</summary>
    public string? Website { get; private set; }

    /// <summary>Observações sobre a conta (opcional).</summary>
    public string? Notes { get; private set; }

    /// <summary>Carimbo de criação (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Carimbo da última atualização (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Eventos de domínio acumulados pelo agregado, ainda não despachados.
    /// Despachados via Outbox após commit pelo <c>TransactionBehavior</c> (DD-007).
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Contatos vinculados à conta. Somente leitura externamente;
    /// mutações passam pelos métodos de comportamento do root.
    /// </summary>
    public IReadOnlyList<Contact> Contacts => _contacts.AsReadOnly();

    // =========================================================================
    // Construtor privado
    // =========================================================================

    private Account() { Name = null!; NormalizedName = null!; } // EF Core

    private Account(
        Guid id,
        Guid tenantId,
        Guid buId,
        AccountName name,
        NormalizedName normalizedName,
        string? website,
        string? notes,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        TenantId = tenantId;
        BuId = buId;
        Name = name;
        NormalizedName = normalizedName;
        Website = website;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    // =========================================================================
    // Factories
    // =========================================================================

    /// <summary>
    /// Cria uma nova conta, calculando a forma normalizada do nome.
    /// Gera o evento <see cref="AccountCreated"/>.
    ///
    /// Mapeia: design §4.1, Req 1, ACC-ERR-001, ADR-0009.
    /// </summary>
    /// <param name="tenantId">Tenant proprietário da conta.</param>
    /// <param name="buId">Business Unit dona da conta — obrigatório e imutável (I4, ADR-0009).</param>
    /// <param name="name">Nome validado da conta (I1).</param>
    /// <param name="website">Website opcional.</param>
    /// <param name="notes">Observações opcionais.</param>
    /// <param name="normalizer">Domain service de normalização (I2).</param>
    /// <returns>Nova instância de <see cref="Account"/> com evento enfileirado.</returns>
    /// <exception cref="ArgumentNullException">
    /// Lançada quando <paramref name="name"/> ou <paramref name="normalizer"/> é nulo.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Lançada quando <paramref name="buId"/> é <see cref="Guid.Empty"/> (I4).
    /// </exception>
    public static Account Create(
        Guid tenantId,
        Guid buId,
        AccountName name,
        string? website,
        string? notes,
        NameNormalizer normalizer)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentNullException.ThrowIfNull(normalizer, nameof(normalizer));

        if (buId == Guid.Empty)
            throw new ArgumentException(
                "O identificador de Business Unit (buId) não pode ser vazio. " +
                "Toda conta deve pertencer a exatamente uma BU (ADR-0009, I4).",
                nameof(buId));

        var now = DateTimeOffset.UtcNow;
        var normalizedName = normalizer.NormalizeName(name.Value);
        var account = new Account(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            buId: buId,
            name: name,
            normalizedName: normalizedName,
            website: website,
            notes: notes,
            createdAt: now,
            updatedAt: now);

        account._domainEvents.Add(new AccountCreated(
            EventId: Guid.NewGuid(),
            AccountId: account.Id,
            TenantId: tenantId,
            NormalizedName: normalizedName.Value,
            OccurredAt: now));

        return account;
    }

    /// <summary>
    /// Reconstitui um agregado <see cref="Account"/> a partir do estado persistido.
    /// Não gera domain events — uso exclusivo do repositório.
    /// </summary>
    public static Account Reconstitute(
        Guid id,
        Guid tenantId,
        Guid buId,
        AccountName name,
        NormalizedName normalizedName,
        string? website,
        string? notes,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IEnumerable<Contact> contacts)
    {
        var account = new Account(
            id, tenantId, buId, name, normalizedName,
            website, notes, createdAt, updatedAt);
        account._contacts.AddRange(contacts);
        return account;
    }

    // =========================================================================
    // Comportamentos
    // =========================================================================

    /// <summary>
    /// Renomeia a conta, recalculando a forma normalizada (I2).
    /// Gera o evento <see cref="AccountUpdated"/>.
    ///
    /// Mapeia: design §4.1, Req 4.2, ACC-ERR-001.
    /// </summary>
    /// <param name="newName">Novo nome validado.</param>
    /// <param name="normalizer">Domain service de normalização.</param>
    public void Rename(AccountName newName, NameNormalizer normalizer)
    {
        ArgumentNullException.ThrowIfNull(newName, nameof(newName));
        ArgumentNullException.ThrowIfNull(normalizer, nameof(normalizer));

        Name = newName;
        NormalizedName = normalizer.NormalizeName(newName.Value);
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new AccountUpdated(
            EventId: Guid.NewGuid(),
            AccountId: Id,
            TenantId: TenantId,
            ChangedFields: ["name", "normalizedName"],
            OccurredAt: UpdatedAt));
    }

    /// <summary>
    /// Adiciona um contato à conta (I3: herda <see cref="TenantId"/> e <see cref="Id"/> do root).
    /// Gera o evento <see cref="ContactLinked"/> com <c>maskedDelta</c> sem PII (DD-003, RNF 1.2).
    ///
    /// Mapeia: design §4.1, Req 5.1, ACC-ERR-004, ACC-ERR-005.
    /// </summary>
    /// <param name="info">Dados de PII do contato (validados).</param>
    /// <param name="role">Cargo opcional do contato.</param>
    public void AddContact(ContactInfo info, string? role)
    {
        ArgumentNullException.ThrowIfNull(info, nameof(info));

        var contact = Contact.Create(TenantId, Id, info, role);
        _contacts.Add(contact);

        var masked = info.ToMasked();
        var maskedDelta = BuildMaskedDelta(masked);

        _domainEvents.Add(new ContactLinked(
            EventId: Guid.NewGuid(),
            ContactId: contact.Id,
            AccountId: Id,
            TenantId: TenantId,
            Action: "created",
            MaskedDelta: maskedDelta,
            OccurredAt: DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Atualiza os dados de PII de um contato existente.
    /// Gera o evento <see cref="ContactLinked"/> com <c>maskedDelta</c> sem PII (DD-003).
    ///
    /// Mapeia: design §4.2, Req 5, ACC-ERR-006.
    /// </summary>
    /// <param name="contactId">Identificador do contato a atualizar.</param>
    /// <param name="newInfo">Novos dados de PII.</param>
    /// <param name="role">Novo cargo (opcional).</param>
    /// <exception cref="InvalidOperationException">Contato não encontrado na conta.</exception>
    public void UpdateContact(Guid contactId, ContactInfo newInfo, string? role)
    {
        ArgumentNullException.ThrowIfNull(newInfo, nameof(newInfo));

        var contact = FindContact(contactId);
        contact.UpdateInfo(newInfo, role);

        var masked = newInfo.ToMasked();
        var maskedDelta = BuildMaskedDelta(masked);

        _domainEvents.Add(new ContactLinked(
            EventId: Guid.NewGuid(),
            ContactId: contact.Id,
            AccountId: Id,
            TenantId: TenantId,
            Action: "updated",
            MaskedDelta: maskedDelta,
            OccurredAt: DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Executa o direito ao esquecimento LGPD para o contato especificado.
    /// Transição irreversível <c>Active → Anonymized</c>; PII substituída por marcadores.
    /// Gera o evento <see cref="ContactForgotten"/> sem PII (RNF 1.2, DD-001).
    ///
    /// Mapeia: design §4.5, Req 7, PBT-03, ACC-ERR-007.
    /// </summary>
    /// <param name="contactId">Identificador do contato a anonimizar.</param>
    /// <param name="requestedBy">Identificador do usuário que solicitou o esquecimento.</param>
    /// <exception cref="InvalidOperationException">Contato não encontrado na conta.</exception>
    /// <exception cref="ContactAlreadyForgottenException">Contato já anonimizado.</exception>
    public void ForgetContact(Guid contactId, Guid requestedBy)
    {
        var contact = FindContact(contactId);
        contact.Forget(requestedBy);

        _domainEvents.Add(new ContactForgotten(
            EventId: Guid.NewGuid(),
            ContactId: contact.Id,
            AccountId: Id,
            TenantId: TenantId,
            RequestedBy: requestedBy,
            OccurredAt: DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Remove todos os domain events acumulados após o despacho via Outbox.
    /// Chamado pelo <c>TransactionBehavior</c> após o commit (DD-007).
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private Contact FindContact(Guid contactId) =>
        _contacts.FirstOrDefault(c => c.Id == contactId)
        ?? throw new InvalidOperationException(
            $"Contato '{contactId}' não encontrado na conta '{Id}'.");

    /// <summary>
    /// Constrói a representação JSON mascarada do delta de PII para auditoria/eventos.
    /// A PII é recebida já mascarada via <see cref="ContactInfo.ToMasked"/>.
    /// </summary>
    private static string BuildMaskedDelta(ContactInfo masked) =>
        $"{{\"name\":\"{masked.Name}\",\"hasEmail\":{masked.Email is not null},\"hasPhone\":{masked.Phone is not null}}}";
}
