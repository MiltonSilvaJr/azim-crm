using System.Net;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Contracts;

namespace NotificationDelivery.Infrastructure.Mapping;

/// <summary>
/// ACL central: traduz toda resposta/exceção do provedor HTTP para
/// <see cref="SendResult"/> canônico (Req 3, Req 7, PBT-04, design §4.6).
///
/// Mapeamento <b>total</b> (PBT-04): nenhuma resposta resulta em estado indefinido
/// ou exceção propagada. <see cref="FailureCode.UnclassifiableProviderResponse"/>
/// é o fallback conservador para qualquer situação não mapeada.
///
/// Nenhuma mensagem de <see cref="FailureReason"/> expõe PII nem credencial (RNF 4, Req 3.3).
/// </summary>
public sealed class ProviderResponseMapper
{
    /// <summary>
    /// Nome do provedor reportado no <see cref="SendResult"/> (sobrescrito pelo sender concreto).
    /// </summary>
    public const string DefaultProvider = "unknown";

    // -------------------------------------------------------------------------
    // Map a partir de ProviderResponse (resposta normalizada do adapter)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Traduz uma <see cref="ProviderResponse"/> normalizada para <see cref="SendResult"/>.
    ///
    /// Caso: resposta inesperada/não mapeada → <see cref="SendStatus.TransientFailure"/>
    /// com <see cref="FailureCode.UnclassifiableProviderResponse"/> (fallback conservador).
    /// </summary>
    /// <param name="response">Resposta normalizada retornada pelo adapter de provedor.</param>
    /// <param name="correlationId">Identificador de correlação da mensagem original.</param>
    /// <param name="provider">Nome do provedor (ex.: "resend", "sendgrid").</param>
    /// <param name="attemptCount">Número de tentativas realizadas.</param>
    /// <returns><see cref="SendResult"/> com status canônico (nunca lança exceção).</returns>
    public SendResult Map(
        ProviderResponse response,
        string correlationId,
        string provider = DefaultProvider,
        int attemptCount = 1)
    {
        if (response.IsSuccess && !string.IsNullOrWhiteSpace(response.MessageId))
        {
            // HTTP 200/202 com message_id → Sent (design §4.5)
            return new SendResult(
                status: SendStatus.Sent,
                correlationId: correlationId,
                provider: provider,
                attemptCount: attemptCount,
                messageId: response.MessageId,
                reason: null);
        }

        // Resposta de falha — usar código canônico do catálogo
        var (status, code, message, isRetriable) = ClassifyFailure(
            response.ErrorCode,
            response.ErrorMessage,
            response.IsRetriable);

        return new SendResult(
            status: status,
            correlationId: correlationId,
            provider: provider,
            attemptCount: attemptCount,
            messageId: null,
            reason: new FailureReason(code, message, IsRetriable: isRetriable));
    }

    // -------------------------------------------------------------------------
    // Map a partir de HttpStatusCode (resposta HTTP bruta do provedor)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Traduz um <see cref="HttpStatusCode"/> e corpo opcional de resposta do provedor
    /// para <see cref="SendResult"/> canônico.
    ///
    /// Regras de mapeamento (design §12, Req 7, PBT-04):
    /// <list type="bullet">
    ///   <item><description>200/202 com <paramref name="messageId"/> → <see cref="SendStatus.Sent"/>.</description></item>
    ///   <item><description>400/422 → <see cref="SendStatus.PermanentFailure"/> / <see cref="FailureCode.ProviderRejectedPayload"/>.</description></item>
    ///   <item><description>401/403 → <see cref="SendStatus.PermanentFailure"/> / <see cref="FailureCode.InvalidOrRevokedCredential"/>.</description></item>
    ///   <item><description>429/5xx → <see cref="SendStatus.TransientFailure"/> / <see cref="FailureCode.TransientProviderFailure"/>.</description></item>
    ///   <item><description>hard_bounce → <see cref="SendStatus.Bounced"/> / <see cref="FailureCode.HardBounce"/>.</description></item>
    ///   <item><description>suppressed → <see cref="SendStatus.Suppressed"/> / <see cref="FailureCode.AddressSuppressed"/>.</description></item>
    ///   <item><description>Qualquer outra resposta → <see cref="SendStatus.TransientFailure"/> / <see cref="FailureCode.UnclassifiableProviderResponse"/>.</description></item>
    /// </list>
    /// </summary>
    /// <param name="statusCode">Código HTTP retornado pelo provedor.</param>
    /// <param name="correlationId">Identificador de correlação da mensagem original.</param>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="attemptCount">Número de tentativas realizadas.</param>
    /// <param name="messageId">ID de mensagem do provedor (presente somente em sucesso).</param>
    /// <param name="isBounce">Indica se a resposta representa hard bounce.</param>
    /// <param name="isSuppressed">Indica se o endereço está na lista de supressão.</param>
    /// <returns><see cref="SendResult"/> com status canônico.</returns>
    public SendResult MapHttpStatus(
        HttpStatusCode statusCode,
        string correlationId,
        string provider = DefaultProvider,
        int attemptCount = 1,
        string? messageId = null,
        bool isBounce = false,
        bool isSuppressed = false)
    {
        // Hard bounce (Req 7.1, NOTIF-ERR-030)
        if (isBounce)
        {
            return new SendResult(
                status: SendStatus.Bounced,
                correlationId: correlationId,
                provider: provider,
                attemptCount: attemptCount,
                messageId: null,
                reason: new FailureReason(
                    FailureCode.HardBounce,
                    "Endereço rejeitado pelo servidor de destino (hard bounce).",
                    IsRetriable: false));
        }

        // Supressão (Req 7.2, NOTIF-ERR-031)
        if (isSuppressed)
        {
            return new SendResult(
                status: SendStatus.Suppressed,
                correlationId: correlationId,
                provider: provider,
                attemptCount: attemptCount,
                messageId: null,
                reason: new FailureReason(
                    FailureCode.AddressSuppressed,
                    "Endereço na lista de supressão do provedor.",
                    IsRetriable: false));
        }

        var intCode = (int)statusCode;

        // Sucesso: 200 ou 202 com messageId (Req 3.1, design §4.5)
        if ((intCode == 200 || intCode == 202) && !string.IsNullOrWhiteSpace(messageId))
        {
            return new SendResult(
                status: SendStatus.Sent,
                correlationId: correlationId,
                provider: provider,
                attemptCount: attemptCount,
                messageId: messageId,
                reason: null);
        }

        // 400 / 422 — payload inválido (NOTIF-ERR-020, design §12)
        if (intCode == 400 || intCode == 422)
        {
            return new SendResult(
                status: SendStatus.PermanentFailure,
                correlationId: correlationId,
                provider: provider,
                attemptCount: attemptCount,
                messageId: null,
                reason: new FailureReason(
                    FailureCode.ProviderRejectedPayload,
                    "Provedor rejeitou a requisição por payload inválido.",
                    IsRetriable: false));
        }

        // 401 / 403 — credencial inválida (NOTIF-ERR-021, design §12)
        if (intCode == 401 || intCode == 403)
        {
            return new SendResult(
                status: SendStatus.PermanentFailure,
                correlationId: correlationId,
                provider: provider,
                attemptCount: attemptCount,
                messageId: null,
                reason: new FailureReason(
                    FailureCode.InvalidOrRevokedCredential,
                    "Credencial do provedor inválida ou revogada.",
                    IsRetriable: false));
        }

        // 429 ou 5xx — falha transiente (NOTIF-ERR-010, design §12, Req 8)
        if (intCode == 429 || intCode >= 500)
        {
            return new SendResult(
                status: SendStatus.TransientFailure,
                correlationId: correlationId,
                provider: provider,
                attemptCount: attemptCount,
                messageId: null,
                reason: new FailureReason(
                    FailureCode.TransientProviderFailure,
                    "Falha transiente do provedor; tentativas esgotadas.",
                    IsRetriable: true));
        }

        // Fallback conservador — resposta inesperada (NOTIF-ERR-090, PBT-04)
        return MapUnclassifiable(correlationId, provider, attemptCount);
    }

    // -------------------------------------------------------------------------
    // MapException — traduz exceções de infraestrutura para SendResult
    // -------------------------------------------------------------------------

    /// <summary>
    /// Traduz uma exceção de infraestrutura (timeout, conexão recusada, etc.) para
    /// <see cref="SendResult"/> canônico sem propagar a exceção ao chamador.
    ///
    /// Casos cobertos:
    /// <list type="bullet">
    ///   <item><description><see cref="TaskCanceledException"/> / <see cref="OperationCanceledException"/> → timeout (NOTIF-ERR-012).</description></item>
    ///   <item><description><see cref="HttpRequestException"/> → falha transiente (NOTIF-ERR-010).</description></item>
    ///   <item><description>Qualquer outra exceção → fallback (NOTIF-ERR-090).</description></item>
    /// </list>
    /// </summary>
    /// <param name="exception">Exceção capturada no adapter de provedor.</param>
    /// <param name="correlationId">Identificador de correlação da mensagem original.</param>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="attemptCount">Número de tentativas realizadas.</param>
    /// <returns><see cref="SendResult"/> com status canônico (nunca lança).</returns>
    public SendResult MapException(
        Exception exception,
        string correlationId,
        string provider = DefaultProvider,
        int attemptCount = 1)
    {
        return exception switch
        {
            // Timeout por tentativa (NOTIF-ERR-012, RNF-3.1)
            TaskCanceledException or OperationCanceledException =>
                new SendResult(
                    status: SendStatus.TransientFailure,
                    correlationId: correlationId,
                    provider: provider,
                    attemptCount: attemptCount,
                    messageId: null,
                    reason: new FailureReason(
                        FailureCode.AttemptTimeout,
                        "Tempo limite da tentativa excedido.",
                        IsRetriable: true)),

            // Falha de rede / HTTP (NOTIF-ERR-010)
            HttpRequestException =>
                new SendResult(
                    status: SendStatus.TransientFailure,
                    correlationId: correlationId,
                    provider: provider,
                    attemptCount: attemptCount,
                    messageId: null,
                    reason: new FailureReason(
                        FailureCode.TransientProviderFailure,
                        "Falha de comunicação com o provedor de e-mail.",
                        IsRetriable: true)),

            // Fallback conservador — exceção não classificada (NOTIF-ERR-090, PBT-04)
            _ => MapUnclassifiable(correlationId, provider, attemptCount)
        };
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fallback ACL conservador (NOTIF-ERR-090, PBT-04).
    /// Garante que nenhuma resposta resulta em estado indefinido.
    /// </summary>
    private static SendResult MapUnclassifiable(string correlationId, string provider, int attemptCount) =>
        new(
            status: SendStatus.TransientFailure,
            correlationId: correlationId,
            provider: provider,
            attemptCount: attemptCount,
            messageId: null,
            reason: new FailureReason(
                FailureCode.UnclassifiableProviderResponse,
                "Resposta não classificável do provedor (fallback ACL conservador).",
                IsRetriable: true));

    /// <summary>
    /// Classifica o triplo (errorCode, errorMessage, isRetriable) de um
    /// <see cref="ProviderResponse"/> para o status canônico correspondente.
    ///
    /// Mapeia os códigos do catálogo <see cref="FailureCode"/> para o status canônico correto.
    /// </summary>
    private static (SendStatus Status, string Code, string Message, bool IsRetriable) ClassifyFailure(
        string? errorCode,
        string? errorMessage,
        bool isRetriable)
    {
        var msg = string.IsNullOrWhiteSpace(errorMessage)
            ? "Falha reportada pelo provedor."
            : errorMessage;

        return errorCode switch
        {
            // Bounce (Req 7.1, NOTIF-ERR-030)
            FailureCode.HardBounce =>
                (SendStatus.Bounced, FailureCode.HardBounce,
                 "Endereço rejeitado pelo servidor de destino (hard bounce).", false),

            // Supressão (Req 7.2, NOTIF-ERR-031)
            FailureCode.AddressSuppressed =>
                (SendStatus.Suppressed, FailureCode.AddressSuppressed,
                 "Endereço na lista de supressão do provedor.", false),

            // Credencial inválida (NOTIF-ERR-021)
            FailureCode.InvalidOrRevokedCredential =>
                (SendStatus.PermanentFailure, FailureCode.InvalidOrRevokedCredential,
                 "Credencial do provedor inválida ou revogada.", false),

            // Payload inválido (NOTIF-ERR-020)
            FailureCode.ProviderRejectedPayload =>
                (SendStatus.PermanentFailure, FailureCode.ProviderRejectedPayload,
                 "Provedor rejeitou a requisição por payload inválido.", false),

            // Secret Manager (NOTIF-ERR-040)
            FailureCode.SecretProviderFailure =>
                (SendStatus.PermanentFailure, FailureCode.SecretProviderFailure,
                 "Falha ao recuperar segredo do provedor.", false),

            // Timeout (NOTIF-ERR-012)
            FailureCode.AttemptTimeout =>
                (SendStatus.TransientFailure, FailureCode.AttemptTimeout,
                 "Tempo limite da tentativa excedido.", true),

            // Circuit breaker (NOTIF-ERR-011)
            FailureCode.CircuitBreakerOpen =>
                (SendStatus.TransientFailure, FailureCode.CircuitBreakerOpen,
                 "Provedor indisponível; circuit breaker aberto.", true),

            // Falha transiente genérica (NOTIF-ERR-010)
            FailureCode.TransientProviderFailure =>
                (SendStatus.TransientFailure, FailureCode.TransientProviderFailure,
                 msg, true),

            // Fallback conservador para código desconhecido (NOTIF-ERR-090, PBT-04)
            _ =>
                (SendStatus.TransientFailure, FailureCode.UnclassifiableProviderResponse,
                 "Resposta não classificável do provedor (fallback ACL conservador).", isRetriable)
        };
    }
}
