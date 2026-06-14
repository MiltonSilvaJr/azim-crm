using AccountManagement.Domain.Shared;

namespace AccountManagement.Domain.Accounts.Events;

/// <summary>
/// Evento de domínio publicado quando uma conta é atualizada (ex.: nome renomeado).
///
/// Carga sem PII (RNF 1.2): lista dos campos alterados (sem valores).
/// O evento é acumulado no agregado e despachado via Outbox após commit (DD-007).
///
/// Mapeia: design §4.4, Req 4.4, Req 8.
/// </summary>
/// <param name="EventId">Identificador único do evento.</param>
/// <param name="AccountId">Identificador da conta atualizada.</param>
/// <param name="TenantId">Tenant ao qual a conta pertence.</param>
/// <param name="ChangedFields">Campos alterados nesta operação (sem valores, sem PII).</param>
/// <param name="OccurredAt">Momento da atualização (UTC).</param>
public sealed record AccountUpdated(
    Guid EventId,
    Guid AccountId,
    Guid TenantId,
    IReadOnlyList<string> ChangedFields,
    DateTimeOffset OccurredAt) : IDomainEvent;
