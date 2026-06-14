using AccountManagement.Domain.Shared;

namespace AccountManagement.Domain.Accounts.Events;

/// <summary>
/// Evento de domínio publicado quando um contato é criado ou atualizado em uma conta.
///
/// <see cref="MaskedDelta"/> contém a representação mascarada das alterações de PII —
/// nunca carrega PII em texto claro (RNF 1.2, DD-003).
/// O mascaramento é aplicado via <c>ContactInfo.ToMasked()</c> antes de enfileirar o evento.
///
/// Mapeia: design §4.4, Req 5.7, Req 8.2.
/// </summary>
/// <param name="EventId">Identificador único do evento.</param>
/// <param name="ContactId">Identificador do contato.</param>
/// <param name="AccountId">Identificador da conta à qual o contato pertence.</param>
/// <param name="TenantId">Tenant ao qual o contato pertence.</param>
/// <param name="Action">Ação que gerou o evento (<c>created</c> ou <c>updated</c>).</param>
/// <param name="MaskedDelta">Representação mascarada das alterações (sem PII em claro).</param>
/// <param name="OccurredAt">Momento do evento (UTC).</param>
public sealed record ContactLinked(
    Guid EventId,
    Guid ContactId,
    Guid AccountId,
    Guid TenantId,
    string Action,
    string MaskedDelta,
    DateTimeOffset OccurredAt) : IDomainEvent;
