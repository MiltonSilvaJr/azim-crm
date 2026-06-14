using System.Text.Json;
using Digest.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Digest.Api.Endpoints;

/// <summary>
/// Extensão de rota para o consumer Pub/Sub push do fan-out por tenant (TASK-22).
/// Registra <c>POST /internal/digest/consume</c> que recebe mensagens push do Pub/Sub.
/// </summary>
public static class PerTenantConsumerEndpoint
{
    /// <summary>Registra o endpoint do consumer no grupo de rotas.</summary>
    public static RouteGroupBuilder MapPerTenantConsumerEndpoint(this RouteGroupBuilder group)
    {
        // Endpoint de recebimento de mensagens push do Pub/Sub (azim-digest-fanout)
        group.MapPost("/consume", HandleAsync)
             .WithName("PerTenantConsumer")
             .WithSummary("Consumer Pub/Sub push — processa RunDigestForTenantCommand por tenant");

        // Endpoint de webhook de status de entrega do provedor (Req 11.1)
        group.MapPost("/delivery-status", HandleDeliveryStatusAsync)
             .WithName("DeliveryStatusWebhook")
             .WithSummary("Webhook de status de entrega do provedor de e-mail");

        return group;
    }

    // -----------------------------------------------------------------------
    // Handler do fan-out Pub/Sub (TASK-22)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Processa mensagem push Pub/Sub do tópico <c>azim-digest-fanout</c>.
    /// Deserializa tenant_id e reference_utc e despacha <see cref="RunDigestForTenantCommand"/>.
    /// Falha de um tenant não propaga exceção ao Pub/Sub (retorna 200 — evita retry infinito).
    /// Após N tentativas de retry, vai para DLQ (azim-digest-fanout-dlq — design §6.3).
    /// </summary>
    private static async Task<IResult> HandleAsync(
        PubSubPushEnvelope envelope,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Deserializa o payload da mensagem Pub/Sub
        FanoutMessage? message;
        try
        {
            var decoded = Convert.FromBase64String(envelope.Message.Data ?? string.Empty);
            var json = System.Text.Encoding.UTF8.GetString(decoded);
            message = JsonSerializer.Deserialize<FanoutMessage>(json, CamelCaseOptions);
        }
        catch (Exception ex)
        {
            // Payload inválido → 400 (Pub/Sub não retenta mensagem com 400 — design §6.3)
            logger.LogWarning(ex, "Consumer: payload Pub/Sub inválido. MessageId: {MessageId}",
                envelope.Message.MessageId);
            return Results.BadRequest(new { error = "Payload inválido", code = "DIG-ERR-001" });
        }

        if (message is null || message.TenantId == Guid.Empty)
        {
            logger.LogWarning("Consumer: mensagem Pub/Sub sem tenant_id válido. MessageId: {MessageId}",
                envelope.Message.MessageId);
            return Results.BadRequest(new { error = "tenant_id ausente ou inválido", code = "DIG-ERR-001" });
        }

        // Log sem PII (RNF 3, DD-011): apenas tenant_id e correlation_id
        logger.LogInformation(
            "Consumer: processando tenant {TenantId}. CorrelationId: {CorrelationId}",
            message.TenantId, message.CorrelationId);

        try
        {
            // Despacha o comando de processamento do tenant
            await mediator.Send(
                new RunDigestForTenantCommand(
                    message.TenantId,
                    message.ReferenceUtc,
                    message.CorrelationId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Falha no processamento de um tenant:
            // Retorna 200 para que o Pub/Sub NÃO retente imediatamente.
            // A retry policy da subscription (com backoff e DLQ) gerencia retentativas (design §6.3, RNF 5.3).
            logger.LogError(ex,
                "Consumer: falha ao processar tenant {TenantId}. CorrelationId: {CorrelationId}. Não propagando erro para Pub/Sub.",
                message.TenantId, message.CorrelationId);
        }

        // 200 OK — Pub/Sub considera mensagem processada (acknowledge)
        return Results.Ok(new { ack = true });
    }

    // -----------------------------------------------------------------------
    // Handler do webhook de status de entrega (Req 11.1)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Processa webhook de evento de entrega do provedor de e-mail.
    /// Atualiza <c>EmailDigestLog</c> via <see cref="UpdateDeliveryStatusCommand"/>.
    /// Idempotente por <c>message_id</c> (design §5.3).
    /// </summary>
    private static async Task<IResult> HandleDeliveryStatusAsync(
        DeliveryStatusWebhookRequest request,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return Results.BadRequest(new { error = "message_id ausente" });
        }

        // Mapeia status do provedor para DigestStatus
        var providerStatus = MapProviderStatus(request.Status);
        if (providerStatus is null)
        {
            // Status desconhecido: ignora graciosamente (idempotente)
            logger.LogWarning("Webhook: status desconhecido '{Status}' para message_id {MessageId}",
                request.Status, request.MessageId);
            return Results.Ok(new { skipped = true, reason = "unknown_status" });
        }

        try
        {
            var result = await mediator.Send(
                new UpdateDeliveryStatusCommand(request.MessageId, providerStatus.Value),
                cancellationToken);

            return Results.Ok(new { updated = result.Updated, skipped = result.Skipped });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Webhook: erro ao atualizar status para message_id {MessageId}",
                request.MessageId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Digest.Domain.Enums.DigestStatus? MapProviderStatus(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "delivered" => Digest.Domain.Enums.DigestStatus.Delivered,
            "opened" or "open" => Digest.Domain.Enums.DigestStatus.Opened,
            "bounced" or "bounce" or "hard_bounce" or "soft_bounce" => Digest.Domain.Enums.DigestStatus.Bounced,
            _ => null,
        };
}

// -----------------------------------------------------------------------
// DTOs de request/response do Pub/Sub push (design §6.3)
// -----------------------------------------------------------------------

/// <summary>
/// Envelope de mensagem push do Pub/Sub (formato padrão do Google Cloud Pub/Sub push).
/// </summary>
public sealed record PubSubPushEnvelope(PubSubMessage Message, string Subscription);

/// <summary>Mensagem individual do Pub/Sub push.</summary>
public sealed record PubSubMessage(
    string? Data,
    string MessageId,
    string? PublishTime,
    IReadOnlyDictionary<string, string>? Attributes);

/// <summary>
/// Payload serializado no campo <c>data</c> (Base64) de cada mensagem Pub/Sub do fan-out.
/// Uma mensagem por tenant elegível (DD-005).
/// </summary>
public sealed record FanoutMessage(
    Guid TenantId,
    DateTimeOffset ReferenceUtc,
    Guid? CorrelationId);

/// <summary>
/// Payload de request do webhook de status de entrega do provedor (Req 11.1).
/// </summary>
public sealed record DeliveryStatusWebhookRequest(
    string? MessageId,
    string? Status,
    string? TenantId);
