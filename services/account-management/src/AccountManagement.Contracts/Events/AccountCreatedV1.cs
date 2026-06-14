namespace AccountManagement.Contracts.Events;

/// <summary>
/// Contrato de evento de integração <c>account.created.v1</c>.
///
/// Publicado quando uma conta é criada com sucesso.
/// Sem PII em texto claro (RNF 1.2, design §9).
/// Envelope completo com <c>event_id</c>, <c>correlation_id</c> e <c>occurred_at</c> (design §9).
///
/// Channel/topic: <c>account-management.account.created</c>.
/// Consumidor: audit-log.
/// Idempotência: dedupe por <see cref="EventId"/> no consumidor.
///
/// Mapeia: design §9, Req 1.7, Req 8.1, ACC-ERR — sem erros neste evento.
/// </summary>
public sealed record AccountCreatedV1(
    /// <summary>Identificador único do evento (para dedupe no consumidor).</summary>
    Guid EventId,
    /// <summary>Versão do contrato.</summary>
    string EventVersion,
    /// <summary>Tipo do evento (constante).</summary>
    string EventType,
    /// <summary>Tenant proprietário da conta.</summary>
    Guid TenantId,
    /// <summary>Identificador da conta criada.</summary>
    Guid AccountId,
    /// <summary>Forma normalizada do nome da conta (sem PII direta — DD-005).</summary>
    string NormalizedName,
    /// <summary>Identificador de correlação da requisição de origem (rastreabilidade).</summary>
    string CorrelationId,
    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTimeOffset OccurredAt)
{
    /// <summary>Tipo do evento — constante estável para deserialização dos consumidores.</summary>
    public const string TypeName = "account.created.v1";

    /// <summary>Versão corrente do contrato.</summary>
    public const string CurrentVersion = "1";
}
