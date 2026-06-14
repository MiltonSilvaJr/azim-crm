using AccountManagement.Domain.Shared;

namespace AccountManagement.Domain.Accounts.Events;

/// <summary>
/// Evento de domínio publicado quando um contato é anonimizado (direito ao esquecimento LGPD).
///
/// Carga sem PII (RNF 1.2): contém apenas identificadores e quem solicitou.
/// O <c>contact_id</c> é preservado para manter integridade referencial (Req 7.3, DD-001).
///
/// Mapeia: design §4.4, Req 7.4.
/// </summary>
/// <param name="EventId">Identificador único do evento.</param>
/// <param name="ContactId">Identificador do contato anonimizado (preservado).</param>
/// <param name="AccountId">Identificador da conta à qual o contato pertencia.</param>
/// <param name="TenantId">Tenant ao qual o contato pertencia.</param>
/// <param name="RequestedBy">Identificador do usuário que solicitou o esquecimento.</param>
/// <param name="OccurredAt">Momento do esquecimento (UTC).</param>
public sealed record ContactForgotten(
    Guid EventId,
    Guid ContactId,
    Guid AccountId,
    Guid TenantId,
    Guid RequestedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;
