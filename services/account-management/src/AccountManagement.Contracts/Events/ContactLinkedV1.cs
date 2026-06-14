namespace AccountManagement.Contracts.Events;

/// <summary>
/// Contrato de evento de integração <c>account.contact_linked.v1</c>.
///
/// Publicado quando um contato é criado ou atualizado em uma conta.
/// <see cref="MaskedDelta"/> carrega o delta de PII mascarado pelo <c>PiiMasker</c> (DD-003),
/// sem PII em texto claro (RNF 1.2).
///
/// Channel/topic: <c>account-management.contact.linked</c>.
/// Consumidor: audit-log.
///
/// Mapeia: design §9, Req 5.7, Req 8.2, DD-003.
/// </summary>
public sealed record ContactLinkedV1(
    /// <summary>Identificador único do evento.</summary>
    Guid EventId,
    /// <summary>Versão do contrato.</summary>
    string EventVersion,
    /// <summary>Tipo do evento (constante).</summary>
    string EventType,
    /// <summary>Tenant proprietário.</summary>
    Guid TenantId,
    /// <summary>Identificador do contato (preservado mesmo após anonimização — DD-001).</summary>
    Guid ContactId,
    /// <summary>Identificador da conta à qual o contato está vinculado.</summary>
    Guid AccountId,
    /// <summary>Ação: "created" ou "updated".</summary>
    string Action,
    /// <summary>
    /// Delta mascarado de PII em JSON — sem valores de PII em claro (DD-003).
    /// Campos: <c>name</c> mascarado, <c>hasEmail</c>, <c>hasPhone</c>.
    /// </summary>
    string MaskedDelta,
    /// <summary>Identificador de correlação.</summary>
    string CorrelationId,
    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTimeOffset OccurredAt)
{
    /// <summary>Tipo do evento — constante estável.</summary>
    public const string TypeName = "account.contact_linked.v1";

    /// <summary>Versão corrente.</summary>
    public const string CurrentVersion = "1";
}
