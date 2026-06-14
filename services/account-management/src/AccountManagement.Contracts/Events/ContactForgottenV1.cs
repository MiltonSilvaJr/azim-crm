namespace AccountManagement.Contracts.Events;

/// <summary>
/// Contrato de evento de integração <c>account.contact_forgotten.v1</c>.
///
/// Publicado quando o direito ao esquecimento LGPD é executado para um contato.
/// Sem PII em texto claro — apenas <see cref="ContactId"/> (referência estável, DD-001)
/// e <see cref="RequestedBy"/> (user_id de auditoria, não é PII sensível — design §9).
///
/// Channel/topic: <c>account-management.contact.forgotten</c>.
/// Consumidor: audit-log.
///
/// Mapeia: design §9, Req 7.4, DD-001, RNF 1.2.
/// </summary>
public sealed record ContactForgottenV1(
    /// <summary>Identificador único do evento.</summary>
    Guid EventId,
    /// <summary>Versão do contrato.</summary>
    string EventVersion,
    /// <summary>Tipo do evento (constante).</summary>
    string EventType,
    /// <summary>Tenant proprietário.</summary>
    Guid TenantId,
    /// <summary>Identificador do contato anonimizado (preservado — DD-001, Req 7.3).</summary>
    Guid ContactId,
    /// <summary>Conta à qual o contato pertence.</summary>
    Guid AccountId,
    /// <summary>Identificador do Tenant Admin que solicitou o esquecimento (auditoria).</summary>
    Guid RequestedBy,
    /// <summary>Identificador de correlação.</summary>
    string CorrelationId,
    /// <summary>Momento em que o evento ocorreu (UTC).</summary>
    DateTimeOffset OccurredAt)
{
    /// <summary>Tipo do evento — constante estável.</summary>
    public const string TypeName = "account.contact_forgotten.v1";

    /// <summary>Versão corrente.</summary>
    public const string CurrentVersion = "1";
}
