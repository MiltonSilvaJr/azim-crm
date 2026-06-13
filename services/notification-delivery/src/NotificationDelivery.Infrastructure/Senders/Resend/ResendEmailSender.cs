using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.Mapping;

namespace NotificationDelivery.Infrastructure.Senders.Resend;

/// <summary>
/// Adapter HTTP para o Resend (provedor primário de e-mail — DD-001, ADR-0005).
///
/// Implementa <see cref="IEmailProviderClient"/> traduzindo <see cref="EmailMessage"/>
/// para o payload da API Resend v1 e retornando <see cref="ProviderResponse"/> normalizado.
///
/// <para>Invariantes (DD-003, RNF 1):</para>
/// <list type="bullet">
///   <item><description>Nenhum tipo do SDK Resend cruza para Application/Contracts.</description></item>
///   <item><description>Credencial lida via <see cref="ISecretProvider"/> (DD-007, Req 10).</description></item>
///   <item><description><see cref="EmailMessage.IdempotencyKey"/> propagada via header <c>Idempotency-Key</c> (DD-005, Req 9).</description></item>
///   <item><description>TLS 1.2+ obrigatório (RNF 4, design §10).</description></item>
///   <item><description>Credencial nunca logada (RNF-6.3).</description></item>
/// </list>
/// </summary>
public sealed class ResendEmailSender : IEmailProviderClient
{
    /// <summary>Nome canônico do provedor reportado em <see cref="ProviderResponse"/> e logs.</summary>
    public const string ProviderName = "resend";

    /// <summary>Nome lógico do segredo no Secret Manager (DD-007, design §6.7).</summary>
    public const string SecretName = "RESEND_API_KEY";

    // -------------------------------------------------------------------------
    // Constantes da API Resend v1
    // -------------------------------------------------------------------------

    private const string ResendApiBaseUrl = "https://api.resend.com";
    private const string SendEmailPath = "/emails";
    private const string AuthorizationScheme = "Bearer";
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    // -------------------------------------------------------------------------
    // Dependências
    // -------------------------------------------------------------------------

    private readonly HttpClient _httpClient;
    private readonly ISecretProvider _secretProvider;
    private readonly ProviderResponseMapper _mapper;
    private readonly ILogger<ResendEmailSender> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Constrói o sender do Resend com as dependências injetadas.
    /// </summary>
    /// <param name="httpClient">
    /// <see cref="HttpClient"/> pré-configurado via <c>IHttpClientFactory</c>
    /// (base URL e TLS configurados na extensão DI).
    /// </param>
    /// <param name="secretProvider">Porta de segredos para ler a API key do Resend (DD-007).</param>
    /// <param name="mapper">ACL de mapeamento de resposta (ProviderResponseMapper).</param>
    /// <param name="logger">Logger estruturado sem PII (RNF 4, RNF-6.3).</param>
    public ResendEmailSender(
        HttpClient httpClient,
        ISecretProvider secretProvider,
        ProviderResponseMapper mapper,
        ILogger<ResendEmailSender> logger)
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
            // Exceção não logada para evitar exposição acidental da mensagem (RNF-6.3)
            _logger.LogError(
                "Falha ao recuperar credencial do Resend. SecretName={SecretName}. " +
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

        // 2. Construir payload JSON da API Resend v1
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
            "Enviando e-mail via Resend. CorrelationId={CorrelationId}, TenantId={TenantId}. (Req 4)",
            message.CorrelationId,
            message.TenantId);

        // 5. Chamar a API Resend e mapear resposta
        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return await ParseResponseAsync(response, message.CorrelationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException httpEx)
        {
            // Falha de rede — não propaga, converte via mapper (Req 3.5)
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
            // Timeout — não propaga, converte via mapper (Req 3.5)
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
            // Cancelamento — não propaga, converte via mapper (Req 3.5)
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
    /// Constrói o payload para a API Resend v1 (POST /emails).
    /// Nenhum tipo Resend atravessa esta fronteira — JSON puro (DD-003, RNF 1).
    /// </summary>
    private static ResendSendEmailPayload BuildPayload(EmailMessage message) =>
        new(
            From: message.TenantId,   // Remetente por tenant — configurável em produção
            To: [message.RecipientEmail],
            Subject: message.Subject,
            Html: message.HtmlBody,
            Text: message.PlainTextBody,
            Headers: null);

    /// <summary>
    /// Interpreta a resposta HTTP do Resend e retorna <see cref="ProviderResponse"/> normalizado.
    /// Casos tratados: 200/201 (sucesso), 4xx (permanente), 5xx/429 (transiente).
    /// </summary>
    private async Task<ProviderResponse> ParseResponseAsync(
        HttpResponseMessage response,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var statusCode = response.StatusCode;
        var intCode = (int)statusCode;

        if (intCode == 200 || intCode == 201)
        {
            // Resend retorna 200/201 com body { "id": "..." }
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = JsonSerializer.Deserialize<ResendSuccessResponse>(body, JsonOptions);
            var messageId = parsed?.Id;

            if (!string.IsNullOrWhiteSpace(messageId))
            {
                _logger.LogInformation(
                    "E-mail enviado com sucesso via Resend. MessageId={MessageId}, " +
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

            // Sucesso sem messageId — fallback (NOTIF-ERR-090)
            _logger.LogWarning(
                "Resend retornou sucesso mas sem messageId. CorrelationId={CorrelationId}. (NOTIF-ERR-090)",
                correlationId);

            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: FailureCode.UnclassifiableProviderResponse,
                ErrorMessage: "Resposta de sucesso sem identificador de mensagem.",
                IsRetriable: true);
        }

        // 4xx permanentes
        if (intCode == 401 || intCode == 403)
        {
            _logger.LogError(
                "Credencial Resend inválida ou sem permissão. Status={StatusCode}, " +
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

        if (intCode == 400 || intCode == 422)
        {
            _logger.LogWarning(
                "Resend rejeitou payload. Status={StatusCode}, CorrelationId={CorrelationId}. (NOTIF-ERR-020)",
                intCode,
                correlationId);

            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: FailureCode.ProviderRejectedPayload,
                ErrorMessage: "Provedor rejeitou a requisição por payload inválido.",
                IsRetriable: false);
        }

        // 429 / 5xx — transiente
        if (intCode == 429 || intCode >= 500)
        {
            _logger.LogWarning(
                "Falha transiente do Resend. Status={StatusCode}, CorrelationId={CorrelationId}. (NOTIF-ERR-010)",
                intCode,
                correlationId);

            return new ProviderResponse(
                IsSuccess: false,
                MessageId: null,
                ErrorCode: FailureCode.TransientProviderFailure,
                ErrorMessage: "Falha transiente do provedor; tentativas esgotadas.",
                IsRetriable: true);
        }

        // Fallback conservador — resposta não mapeada (NOTIF-ERR-090, PBT-04)
        _logger.LogWarning(
            "Resposta inesperada do Resend. Status={StatusCode}, CorrelationId={CorrelationId}. (NOTIF-ERR-090)",
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

    /// <summary>
    /// Payload de envio para a API Resend v1 (POST /emails).
    /// Tipo interno — nunca cruza a fronteira de Application ou Contracts.
    /// </summary>
    private sealed record ResendSendEmailPayload(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("headers")] Dictionary<string, string>? Headers);

    /// <summary>
    /// Resposta de sucesso da API Resend v1: <c>{ "id": "..." }</c>.
    /// Tipo interno — nunca cruza a fronteira de Application ou Contracts.
    /// </summary>
    private sealed record ResendSuccessResponse(
        [property: JsonPropertyName("id")] string? Id);
}
