namespace NotificationDelivery.Contracts;

/// <summary>
/// Catálogo de códigos de erro canônicos do módulo notification-delivery.
///
/// Todos os 11 códigos definidos no design §12. Usar estas constantes elimina strings
/// mágicas ao construir <see cref="FailureReason"/>.
///
/// Regras (design §12):
/// <list type="bullet">
///   <item><description>Nenhum código expõe o e-mail do destinatário nem credencial.</description></item>
///   <item><description>Erros são estáveis e rastreáveis por <c>correlation_id</c>.</description></item>
///   <item><description>O mapeamento é total (PBT-04) — <see cref="UnclassifiableProviderResponse"/> é o fallback conservador.</description></item>
/// </list>
/// </summary>
public static class FailureCode
{
    // -------------------------------------------------------------------------
    // Erros de validação de borda (design §12, Req 2.5)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Destinatário ausente ou malformado — validação de borda de <c>EmailMessage</c>.
    /// Mapeia para: <see cref="SendStatus.PermanentFailure"/>.
    /// Req 2.5, design §12.
    /// </summary>
    public const string InvalidRecipient = "NOTIF-ERR-001";

    /// <summary>
    /// Assunto ou corpo obrigatório ausente — validação de <c>Subject</c>/<c>HtmlBody</c>.
    /// Mapeia para: <see cref="SendStatus.PermanentFailure"/>.
    /// Design §12.
    /// </summary>
    public const string MissingSubjectOrBody = "NOTIF-ERR-002";

    // -------------------------------------------------------------------------
    // Erros de falha transiente do provedor (design §12, Req 8)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Falha transiente do provedor; tentativas esgotadas (timeout, 5xx, rate limit após N retries).
    /// Mapeia para: <see cref="SendStatus.TransientFailure"/>.
    /// Req 8, design §12.
    /// </summary>
    public const string TransientProviderFailure = "NOTIF-ERR-010";

    /// <summary>
    /// Provedor indisponível; circuit breaker aberto após N falhas consecutivas.
    /// Mapeia para: <see cref="SendStatus.TransientFailure"/>.
    /// Req 8.3, design §12.
    /// </summary>
    public const string CircuitBreakerOpen = "NOTIF-ERR-011";

    /// <summary>
    /// Tempo limite da tentativa excedido (timeout por tentativa RNF-3.1).
    /// Mapeia para: <see cref="SendStatus.TransientFailure"/>.
    /// RNF-3.1, design §12.
    /// </summary>
    public const string AttemptTimeout = "NOTIF-ERR-012";

    // -------------------------------------------------------------------------
    // Erros de rejeição permanente do provedor (design §12)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Provedor rejeitou a requisição por payload inválido (4xx não recuperável, ex.: HTTP 400/422).
    /// Mapeia para: <see cref="SendStatus.PermanentFailure"/>.
    /// Design §12.
    /// </summary>
    public const string ProviderRejectedPayload = "NOTIF-ERR-020";

    /// <summary>
    /// Credencial do provedor inválida ou revogada (HTTP 401/403).
    /// Mapeia para: <see cref="SendStatus.PermanentFailure"/>.
    /// Design §12.
    /// </summary>
    public const string InvalidOrRevokedCredential = "NOTIF-ERR-021";

    // -------------------------------------------------------------------------
    // Bounce e supressão (design §12, Req 7)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Endereço rejeitado pelo servidor de destino (hard bounce).
    /// Mapeia para: <see cref="SendStatus.Bounced"/>.
    /// Req 7.1, design §12.
    /// </summary>
    public const string HardBounce = "NOTIF-ERR-030";

    /// <summary>
    /// Endereço na lista de supressão do provedor.
    /// Mapeia para: <see cref="SendStatus.Suppressed"/>.
    /// Req 7.2, design §12.
    /// </summary>
    public const string AddressSuppressed = "NOTIF-ERR-031";

    // -------------------------------------------------------------------------
    // Falha de Secret Manager (design §12, Req 10)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Falha ao recuperar segredo do provedor (erro de acesso ao Secret Manager).
    /// Mapeia para: <see cref="SendStatus.PermanentFailure"/>.
    /// Req 10, design §12.
    /// </summary>
    public const string SecretProviderFailure = "NOTIF-ERR-040";

    // -------------------------------------------------------------------------
    // Fallback ACL conservador (design §12, PBT-04)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resposta não classificável do provedor (fallback ACL conservador).
    /// Garante mapeamento total (PBT-04): nenhuma resposta resulta em estado indefinido.
    /// Mapeia para: <see cref="SendStatus.TransientFailure"/>.
    /// Design §12, PBT-04.
    /// </summary>
    public const string UnclassifiableProviderResponse = "NOTIF-ERR-090";
}
