namespace Organization.Contracts.Events;

/// <summary>
/// Envelope padrão para eventos de integração publicados pelo módulo <c>organization</c>.
/// Contém os campos obrigatórios de rastreabilidade (DD-005, §9): <c>tenant_id</c>,
/// <c>correlation_id</c>, <c>message_id</c> e <c>occurred_at</c>.
/// </summary>
/// <param name="MessageId">Identificador único da mensagem (idempotência no consumidor).</param>
/// <param name="EventType">Tipo do evento de integração no formato <c>noun.verb.v1</c> (ex.: <c>bu.created.v1</c>).</param>
/// <param name="TenantId">Identificador do tenant dono do evento.</param>
/// <param name="CorrelationId">Identificador de correlação para rastreabilidade distribuída.</param>
/// <param name="OccurredAt">Instante de ocorrência do evento no domínio (UTC).</param>
/// <param name="Payload">Carga do evento serializada como dicionário de campos. Sem PII (RNF 3).</param>
public sealed record IntegrationEventEnvelope(
    Guid MessageId,
    string EventType,
    Guid TenantId,
    Guid CorrelationId,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, object?> Payload);
