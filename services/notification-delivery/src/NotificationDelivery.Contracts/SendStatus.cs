namespace NotificationDelivery.Contracts;

/// <summary>
/// Estado canônico do resultado de uma tentativa de envio de e-mail.
///
/// Cinco valores terminais — cada chamada <c>IEmailSender.SendAsync</c> resolve
/// em exatamente um destes estados (design §4.3, §4.5, Req 3, Req 7).
///
/// Regras de classificação (design §4.5):
/// <list type="bullet">
///   <item><description><see cref="Bounced"/> e <see cref="Suppressed"/> não contam como falha de provedor para o circuit breaker (Req 7.3).</description></item>
///   <item><description><see cref="TransientFailure"/> é elegível a nova tentativa pelo chamador; <see cref="PermanentFailure"/> não.</description></item>
/// </list>
/// </summary>
public enum SendStatus
{
    /// <summary>
    /// Provedor aceitou a mensagem com confirmação de <c>message_id</c>.
    /// </summary>
    Sent,

    /// <summary>
    /// Falha temporária — timeout, 5xx ou rate limit após esgotamento de retries internos,
    /// ou circuit breaker aberto. O chamador pode re-tentar mais tarde.
    /// Mapeia: <see cref="FailureCode.TransientProviderFailure"/>, <see cref="FailureCode.CircuitBreakerOpen"/>, <see cref="FailureCode.AttemptTimeout"/>, <see cref="FailureCode.UnclassifiableProviderResponse"/>.
    /// </summary>
    TransientFailure,

    /// <summary>
    /// Falha permanente — validação de borda, payload inválido (4xx não recuperável) ou
    /// credencial inválida/expirada. O chamador não deve re-tentar sem corrigir a causa raiz.
    /// Mapeia: <see cref="FailureCode.InvalidRecipient"/>, <see cref="FailureCode.MissingSubjectOrBody"/>,
    /// <see cref="FailureCode.ProviderRejectedPayload"/>, <see cref="FailureCode.InvalidOrRevokedCredential"/>,
    /// <see cref="FailureCode.SecretProviderFailure"/>.
    /// </summary>
    PermanentFailure,

    /// <summary>
    /// Endereço rejeitado pelo servidor de destino (hard bounce).
    /// Não contabilizado no circuit breaker (design §4.5, Req 7.3).
    /// Mapeia: <see cref="FailureCode.HardBounce"/>.
    /// </summary>
    Bounced,

    /// <summary>
    /// Endereço na lista de supressão do provedor.
    /// Não contabilizado no circuit breaker (design §4.5, Req 7.3).
    /// Mapeia: <see cref="FailureCode.AddressSuppressed"/>.
    /// </summary>
    Suppressed
}
