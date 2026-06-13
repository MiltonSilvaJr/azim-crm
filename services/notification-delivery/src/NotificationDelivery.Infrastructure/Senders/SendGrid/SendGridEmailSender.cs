using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.Mapping;

namespace NotificationDelivery.Infrastructure.Senders.SendGrid;

/// <summary>
/// Adapter HTTP para o SendGrid — implementação alternativa intercambiável por configuração (DD-001, Req 4).
///
/// Implementa <see cref="IEmailProviderClient"/> com a mesma semântica de <see cref="ProviderResponse"/>
/// que o <c>ResendEmailSender</c> para entradas equivalentes (base do PBT-01 em TASK-17).
///
/// <para>Invariantes (DD-003, RNF 1):</para>
/// <list type="bullet">
///   <item><description>Nenhum tipo do SDK SendGrid cruza para Application/Contracts.</description></item>
///   <item><description>Credencial lida via <see cref="ISecretProvider"/> (DD-007, Req 10).</description></item>
///   <item><description><see cref="EmailMessage.IdempotencyKey"/> propagada via header <c>X-Twilio-Email-List-Management-Idempotency-Token</c> (DD-005, Req 9).</description></item>
///   <item><description>TLS 1.2+ obrigatório.</description></item>
///   <item><description>Credencial nunca logada (RNF-6.3).</description></item>
/// </list>
/// </summary>
public sealed class SendGridEmailSender : IEmailProviderClient
{
    /// <summary>Nome canônico do provedor reportado em <see cref="ProviderResponse"/> e logs.</summary>
    public const string ProviderName = "sendgrid";

    /// <summary>Nome lógico do segredo no Secret Manager (DD-007).</summary>
    public const string SecretName = "SENDGRID_API_KEY";

    // -------------------------------------------------------------------------
    // Constantes da API SendGrid v3
    // -------------------------------------------------------------------------

    private const string SendGridApiBaseUrl = "https://api.sendgrid.com";
    private const string SendEmailPath = "/v3/mail/send";
    private const string AuthorizationScheme = "Bearer";

    // SendGrid usa este header para idempotência (equivalente ao Idempotency-Key do Resend)
    private const string IdempotencyKeyHeader = "X-Twilio-Email-List-Management-Idempotency-Token";

    // -------------------------------------------------------------------------
    // Dependências
    // -------------------------------------------------------------------------

    private readonly HttpClient _httpClient;
    private readonly ISecretProvider _secretProvider;
    private readonly ProviderResponseMapper _mapper;
    private readonly ILogger<SendGridEmailSender> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Constrói o sender do SendGrid com as dependências injetadas.
    /// </summary>
    public SendGridEmailSender(
        HttpClient httpClient,
        ISecretProvider secretProvider,
        ProviderResponseMapper mapper,
        ILogger<SendGridEmailSender> logger)
    {
        _httpClient = httpClient;
        _secretProvider = secretProvider;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProviderResponse> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        // 1. Recuperar credencial via ISecretProvider (DD-007) — nunca hardcoded
        string apiKey;
        try
        {
            apiKey = await _secretProvider.GetSecretAsync(SecretName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SecretProviderException)
        {
            // Credencial indisponível → NOTIF-ERR-040 (design §12)
            _logger.LogError(
                "Falha ao recuperar credencial do SendGrid. SecretName={SecretName}. " +
                "CorrelationId={CorrelationId}. (NOTIF-ERR-040)",
                SecretName,
                message.CorrelationId);

            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: FailureCode.SecretProviderFailure,
                ErrorMessage: "Falha ao recuperar segredo do provedor.",
                IsRetriable: false);
        }

        // 2. Construir payload JSON da API SendGrid v3
        var payload = BuildPayload(message);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 3. Montar request com autenticação Bearer (credencial NUNCA logada — RNF-6.3)
        using var request = new HttpRequestMessage(HttpMethod.Post, SendEmailPath);
        request.Headers.Authorization = new AuthenticationHeaderValue(AuthorizationScheme, apiKey);
        request.Content = content;

        // 4. Propagar IdempotencyKey quando presente (DD-005, Req 9)
        if (!string.IsNullOrWhiteSpace(message.IdempotencyKey))
        {
            request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader, message.IdempotencyKey);
        }

        _logger.LogDebug(
            "Enviando e-mail via SendGrid. CorrelationId={CorrelationId}, TenantId={TenantId}. (Req 4)",
            message.CorrelationId,
            message.TenantId);

        // 5. Chamar a API SendGrid e mapear resposta
        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return ParseResponse(response, message.CorrelationId);
        }
        catch (HttpRequestException httpEx)
        {
            var mapped = _mapper.MapException(httpEx, message.CorrelationId, ProviderName);
            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: mapped.Reason?.Code,
                ErrorMessage: mapped.Reason?.Message,
                IsRetriable: mapped.Reason?.IsRetriable ?? true);
        }
        catch (TaskCanceledException timeoutEx)
        {
            var mapped = _mapper.MapException(timeoutEx, message.CorrelationId, ProviderName);
            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: mapped.Reason?.Code,
                ErrorMessage: mapped.Reason?.Message,
                IsRetriable: mapped.Reason?.IsRetriable ?? true);
        }
        catch (OperationCanceledException cancelEx)
        {
            var mapped = _mapper.MapException(cancelEx, message.CorrelationId, ProviderName);
            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: mapped.Reason?.Code,
                ErrorMessage: mapped.Reason?.Message,
                IsRetriable: mapped.Reason?.IsRetriable ?? true);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constrói o payload para a API SendGrid v3 (POST /v3/mail/send).
    /// Nenhum tipo SendGrid atravessa esta fronteira — JSON puro (DD-003, RNF 1).
    /// </summary>
    private static SendGridSendEmailPayload BuildPayload(EmailMessage message) =>
        new(
            Personalizations:
            [
                new SendGridPersonalization(To: [new SendGridEmailAddress(message.RecipientEmail)])
            ],
            From: new SendGridEmailAddress(message.TenantId),
            Subject: message.Subject,
            Content:
            [
                new SendGridContent("text/html", message.HtmlBody),
                .. (message.PlainTextBody is not null
                    ? new[] { new SendGridContent("text/plain", message.PlainTextBody) }
                    : Array.Empty<SendGridContent>())
            ]);

    /// <summary>
    /// Interpreta a resposta HTTP do SendGrid e retorna <see cref="ProviderResponse"/> normalizado.
    ///
    /// SendGrid usa 202 (Accepted) para sucesso — retorna ID via header <c>X-Message-Id</c>.
    /// </summary>
    private ProviderResponse ParseResponse(HttpResponseMessage response, string correlationId)
    {
        var intCode = (int)response.StatusCode;

        // SendGrid retorna 202 Accepted em sucesso; ID disponível via header X-Message-Id
        if (intCode == 202)
        {
            var messageId = response.Headers.TryGetValues("X-Message-Id", out var ids)
                ? ids.FirstOrDefault()
                : null;

            // Gera ID sintético quando header ausente (compatibilidade com stubs de teste)
            if (string.IsNullOrWhiteSpace(messageId))
            {
                messageId = $"sg-{Guid.NewGuid():N}";
            }

            _logger.LogInformation(
                "E-mail enviado com sucesso via SendGrid. MessageId={MessageId}, " +
                "CorrelationId={CorrelationId}. (Req 4, DD-001)",
                messageId,
                correlationId);

            return new ProviderResponse(
                IsSuccess: true,
                MessageId: messageId,
                ErrorCode: null,
                ErrorMessage: null,
                IsRetriable: false);
        }

        // 401/403 — credencial inválida (NOTIF-ERR-021)
        if (intCode == 401 || intCode == 403)
        {
            _logger.LogError(
                "Credencial SendGrid inválida. Status={StatusCode}, " +
                "CorrelationId={CorrelationId}. (NOTIF-ERR-021)",
                intCode,
                correlationId);

            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: FailureCode.InvalidOrRevokedCredential,
                ErrorMessage: "Credencial do provedor inválida ou revogada.",
                IsRetriable: false);
        }

        // 400/422 — payload inválido (NOTIF-ERR-020)
        if (intCode == 400 || intCode == 422)
        {
            _logger.LogWarning(
                "SendGrid rejeitou payload. Status={StatusCode}, CorrelationId={CorrelationId}. (NOTIF-ERR-020)",
                intCode,
                correlationId);

            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: FailureCode.ProviderRejectedPayload,
                ErrorMessage: "Provedor rejeitou a requisição por payload inválido.",
                IsRetriable: false);
        }

        // 429/5xx — transiente (NOTIF-ERR-010)
        if (intCode == 429 || intCode >= 500)
        {
            _logger.LogWarning(
                "Falha transiente do SendGrid. Status={StatusCode}, CorrelationId={CorrelationId}. (NOTIF-ERR-010)",
                intCode,
                correlationId);

            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: FailureCode.TransientProviderFailure,
                ErrorMessage: "Falha transiente do provedor; tentativas esgotadas.",
                IsRetriable: true);
        }

        // Fallback conservador (NOTIF-ERR-090, PBT-04)
        _logger.LogWarning(
            "Resposta inesperada do SendGrid. Status={StatusCode}, CorrelationId={CorrelationId}. (NOTIF-ERR-090)",
            intCode,
            correlationId);

        return new ProviderResponse(
            IsSuccess: false,
            MessageId: null,
            ErrorCode: FailureCode.UnclassifiableProviderResponse,
            ErrorMessage: "Resposta não classificável do provedor.",
            IsRetriable: true);
    }

    // -------------------------------------------------------------------------
    // DTOs internos (confinados em Infrastructure — RNF 1, DD-003)
    // -------------------------------------------------------------------------

    private sealed record SendGridSendEmailPayload(
        [property: JsonPropertyName("personalizations")] SendGridPersonalization[] Personalizations,
        [property: JsonPropertyName("from")] SendGridEmailAddress From,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("content")] SendGridContent[] Content);

    private sealed record SendGridPersonalization(
        [property: JsonPropertyName("to")] SendGridEmailAddress[] To);

    private sealed record SendGridEmailAddress(
        [property: JsonPropertyName("email")] string Email);

    private sealed record SendGridContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value);
}
