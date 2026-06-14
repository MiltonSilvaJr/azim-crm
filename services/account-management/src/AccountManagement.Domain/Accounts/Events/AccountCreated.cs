using AccountManagement.Domain.Shared;

namespace AccountManagement.Domain.Accounts.Events;

/// <summary>
/// Evento de domínio publicado quando uma conta é criada.
///
/// Carga sem PII (RNF 1.2): contém apenas identificadores e a forma normalizada do nome.
/// O evento é acumulado no agregado e despachado via Outbox após commit (DD-007).
///
/// Mapeia: design §4.4, Req 1.7, Req 8.1.
/// </summary>
/// <param name="EventId">Identificador único do evento (deduplicação — DD-007).</param>
/// <param name="AccountId">Identificador da conta criada.</param>
/// <param name="TenantId">Tenant ao qual a conta pertence.</param>
/// <param name="NormalizedName">Forma normalizada do nome (sem PII).</param>
/// <param name="OccurredAt">Momento da criação (UTC).</param>
public sealed record AccountCreated(
    Guid EventId,
    Guid AccountId,
    Guid TenantId,
    string NormalizedName,
    DateTimeOffset OccurredAt) : IDomainEvent;
