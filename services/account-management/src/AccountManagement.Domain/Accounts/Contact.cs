using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.ValueObjects;

namespace AccountManagement.Domain.Accounts;

/// <summary>
/// Entidade interna do agregado <see cref="Account"/> que representa um contato (pessoa física).
///
/// Identidade: <see cref="Id"/> (UUID), estável e preservada mesmo após anonimização
/// para manter integridade referencial com opportunity-pipeline e activity-management
/// (Req 7.3, PBT-03, DD-001).
///
/// Dados de PII encapsulados em <see cref="ContactInfo"/> — nunca exponha diretamente
/// em logs, traces, eventos ou exceções. Use <see cref="ContactInfo.ToMasked"/>.
///
/// Operações de comportamento são invocadas pelo root <see cref="Account"/>:
/// - <see cref="UpdateInfo"/> — valida e substitui PII (Req 5).
/// - <see cref="Forget"/> — transição irreversível <c>Active → Anonymized</c> (Req 7, DD-001).
///
/// Mapeia: design §4.2, Req 5, Req 7, PBT-03.
/// </summary>
public sealed class Contact
{
    // =========================================================================
    // Identidade e vínculo com o agregado
    // =========================================================================

    /// <summary>Identificador único do contato. Preservado mesmo após anonimização (Req 7.3).</summary>
    public Guid Id { get; private set; }

    /// <summary>Tenant ao qual o contato pertence (herdado do root Account — I3).</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Identificador da conta à qual o contato está vinculado (herdado do root — I3).</summary>
    public Guid AccountId { get; private set; }

    // =========================================================================
    // PII e estado de privacidade
    // =========================================================================

    /// <summary>
    /// Dados de PII do contato. Após anonimização, os campos de PII são substituídos
    /// por <see cref="ContactInfo.AnonymizationMarker"/> (DD-001).
    /// Nunca acesse diretamente em logs — use <see cref="ContactInfo.ToMasked"/>.
    /// </summary>
    public ContactInfo Info { get; private set; }

    /// <summary>Cargo/papel do contato na conta (não é PII sensível).</summary>
    public string? Role { get; private set; }

    /// <summary>Estado de privacidade do contato (Active ou Anonymized).</summary>
    public ContactPrivacyState PrivacyState { get; private set; }

    /// <summary>Momento em que o esquecimento foi solicitado; <c>null</c> quando ativo.</summary>
    public DateTimeOffset? ForgottenAt { get; private set; }

    /// <summary>Identificador do usuário que solicitou o esquecimento; <c>null</c> quando ativo.</summary>
    public Guid? ForgottenBy { get; private set; }

    /// <summary>Carimbo de criação (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Carimbo da última atualização (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    // =========================================================================
    // Construtor privado — criação controlada pelo root Account
    // =========================================================================

    private Contact() { Info = null!; PrivacyState = ContactPrivacyState.Active; } // EF Core

    private Contact(
        Guid id,
        Guid tenantId,
        Guid accountId,
        ContactInfo info,
        string? role,
        ContactPrivacyState privacyState,
        DateTimeOffset? forgottenAt,
        Guid? forgottenBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        TenantId = tenantId;
        AccountId = accountId;
        Info = info;
        Role = role;
        PrivacyState = privacyState;
        ForgottenAt = forgottenAt;
        ForgottenBy = forgottenBy;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    // =========================================================================
    // Factories — invocadas apenas pelo root Account
    // =========================================================================

    /// <summary>
    /// Cria um novo contato ativo vinculado à conta.
    /// Invocado exclusivamente por <see cref="Account.AddContact"/>.
    /// </summary>
    internal static Contact Create(
        Guid tenantId,
        Guid accountId,
        ContactInfo info,
        string? role)
    {
        var now = DateTimeOffset.UtcNow;
        return new Contact(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            accountId: accountId,
            info: info,
            role: role,
            privacyState: ContactPrivacyState.Active,
            forgottenAt: null,
            forgottenBy: null,
            createdAt: now,
            updatedAt: now);
    }

    /// <summary>
    /// Reconstitui um contato a partir do estado persistido (usado pelo repositório).
    /// Não gera domain events.
    /// </summary>
    internal static Contact Reconstitute(
        Guid id,
        Guid tenantId,
        Guid accountId,
        ContactInfo info,
        string? role,
        ContactPrivacyState privacyState,
        DateTimeOffset? forgottenAt,
        Guid? forgottenBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Contact(
            id, tenantId, accountId, info, role,
            privacyState, forgottenAt, forgottenBy, createdAt, updatedAt);
    }

    // =========================================================================
    // Comportamentos — invocados pelo root Account
    // =========================================================================

    /// <summary>
    /// Atualiza os dados de PII do contato.
    /// Invocado exclusivamente por <see cref="Account.UpdateContact"/>.
    /// </summary>
    /// <param name="newInfo">Novos dados de PII (validados).</param>
    /// <param name="newRole">Novo cargo (opcional).</param>
    internal void UpdateInfo(ContactInfo newInfo, string? newRole)
    {
        Info = newInfo;
        Role = newRole;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Executa a transição irreversível <c>Active → Anonymized</c> (direito ao esquecimento LGPD).
    ///
    /// Substitui PII por <see cref="ContactInfo.AnonymizationMarker"/>;
    /// preserva <see cref="Id"/> para integridade referencial (Req 7.3, DD-001).
    ///
    /// Invocado exclusivamente por <see cref="Account.ForgetContact"/>.
    /// </summary>
    /// <param name="requestedBy">Usuário que solicitou o esquecimento.</param>
    /// <exception cref="ContactAlreadyForgottenException">
    /// Lançada quando o contato já está no estado <see cref="ContactPrivacyState.Anonymized"/>.
    /// </exception>
    internal void Forget(Guid requestedBy)
    {
        if (PrivacyState.IsAnonymized)
            throw new ContactAlreadyForgottenException();

        Info = ContactInfo.Create(ContactInfo.AnonymizationMarker);
        Role = null; // Preserva cargo? Optamos por limpar para minimizar PII
        PrivacyState = ContactPrivacyState.Anonymized;
        ForgottenAt = DateTimeOffset.UtcNow;
        ForgottenBy = requestedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
