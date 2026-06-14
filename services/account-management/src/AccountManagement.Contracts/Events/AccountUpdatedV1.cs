namespace AccountManagement.Contracts.Events;

/// <summary>
/// Contrato de evento de integração <c>account.updated.v1</c>.
///
/// Publicado quando uma conta é atualizada.
/// Sem PII em texto claro — <see cref="ChangedFields"/> lista apenas nomes de campos (RNF 1.2).
///
/// Channel/topic: <c>account-management.account.updated</c>.
/// Consumidor: audit-log.
///
/// Mapeia: design §9, Req 4.4, Req 8.
/// </summary>
public sealed record AccountUpdatedV1(
    /// <summary>Identificador único do evento.</summary>
    Guid EventId,
    /// <summary>Versão do contrato.</summary>
    string EventVersion,
    /// <summary>Tipo do evento (constante).</summary>
    string EventType,
    /// <summary>Tenant proprietário da conta.</summary>
    Guid TenantId,
    /// <summary>Identificador da conta atualizada.</summary>
    Guid AccountId,
    /// <summary>Nomes dos campos alterados (sem valores de PII).</summary>
    IReadOnlyList<string> ChangedFields,
    /// <summary>Identificador de correlação.</summary>
    string CorrelationId,
    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTimeOffset OccurredAt)
{
    /// <summary>Tipo do evento — constante estável.</summary>
    public const string TypeName = "account.updated.v1";

    /// <summary>Versão corrente.</summary>
    public const string CurrentVersion = "1";
}
