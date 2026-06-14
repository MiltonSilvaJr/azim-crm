using Digest.Api.Infrastructure;
using Digest.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Digest.Api.Endpoints;

/// <summary>
/// Extensão de rota para o endpoint interno do trigger do digest (TASK-21).
/// Registra <c>POST /internal/digest/trigger</c> com autenticação OIDC/WIF (design §8.1).
/// </summary>
public static class DigestTriggerEndpoint
{
    /// <summary>Registra o endpoint de trigger no grupo de rotas.</summary>
    public static RouteGroupBuilder MapDigestTriggerEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/trigger", HandleAsync)
             .WithName("DigestTrigger")
             .WithSummary("Dispara o ciclo horário do digest (Cloud Scheduler → OIDC/WIF)")
             .RequireAuthorization(TriggerAuthorizationPolicy.Name);

        return group;
    }

    // -----------------------------------------------------------------------
    // Handler do endpoint
    // -----------------------------------------------------------------------

    /// <summary>
    /// Processa o disparo do digest:
    /// 1. Valida o payload (reference_utc opcional).
    /// 2. Seleciona tenants elegíveis.
    /// 3. Publica uma mensagem Pub/Sub por tenant elegível (fan-out — DD-005).
    /// 4. Responde 202 Accepted ANTES da conclusão dos envios (RNF 8.1).
    /// </summary>
    private static async Task<IResult> HandleAsync(
        TriggerRequest? body,
        IMediator mediator,
        ILogger<Program> logger,
        IPubSubFanout fanout,
        CancellationToken cancellationToken)
    {
        // Resolve reference_utc: usa o valor do body ou hora cheia atual (Req 1.5)
        DateTimeOffset referenceUtc;
        if (body?.ReferenceUtc is not null)
        {
            if (!DateTimeOffset.TryParse(body.ReferenceUtc, null,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out referenceUtc))
            {
                // Payload inválido → 400 DIG-ERR-001
                return Results.Problem(
                    title: "Payload inválido",
                    detail: "O campo 'reference_utc' está malformado. Use o formato ISO 8601 (ex.: 2026-06-14T10:00:00Z).",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["error_code"] = "DIG-ERR-001" });
            }
        }
        else
        {
            // Sem reference_utc: usa a hora cheia atual em UTC (Req 1.5)
            var now = DateTimeOffset.UtcNow;
            referenceUtc = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        }

        var correlationId = Guid.NewGuid();

        // Log sem PII: apenas correlationId e referenceUtc (RNF 3, DD-011)
        logger.LogInformation(
            "Trigger do digest recebido. CorrelationId: {CorrelationId}, ReferenceUtc: {ReferenceUtc}",
            correlationId, referenceUtc);

        // Seleciona tenants elegíveis (query cross-tenant — design §5.2)
        IReadOnlyList<Guid> eligibleTenants;
        try
        {
            eligibleTenants = await mediator.Send(
                new SelectEligibleTenantsQuery(referenceUtc), cancellationToken);
        }
        catch (Exception ex)
        {
            // DIG-ERR-020: falha na seleção de tenants
            logger.LogError(ex,
                "Falha ao selecionar tenants elegíveis — DIG-ERR-020. CorrelationId: {CorrelationId}",
                correlationId);
            // Retorna 202 mesmo assim: o Scheduler não deve retentar indefinidamente por falha interna
            return Results.Accepted(value: new TriggerAcceptedResponse(
                Accepted: true,
                EligibleTenants: 0,
                CorrelationId: correlationId));
        }

        // Publica mensagem Pub/Sub por tenant elegível (fan-out DD-005)
        // O processamento dos envios ocorre de forma assíncrona — não bloqueia esta resposta (RNF 8.1)
        await fanout.PublishFanoutMessagesAsync(eligibleTenants, referenceUtc, correlationId, cancellationToken);

        logger.LogInformation(
            "Trigger do digest enfileirado. CorrelationId: {CorrelationId}, EligibleTenants: {Count}",
            correlationId, eligibleTenants.Count);

        // 202 Accepted — devolvido ANTES da conclusão dos envios (RNF 8.1, DD-005)
        return Results.Accepted(value: new TriggerAcceptedResponse(
            Accepted: true,
            EligibleTenants: eligibleTenants.Count,
            CorrelationId: correlationId));
    }
}

/// <summary>
/// Payload de request do trigger (design §8.1).
/// </summary>
/// <param name="ReferenceUtc">Instante UTC do disparo. Opcional; ausência usa hora cheia atual (Req 1.5).</param>
public sealed record TriggerRequest(string? ReferenceUtc);

/// <summary>
/// Resposta 202 Accepted do trigger (design §8.1, RNF 8.1).
/// </summary>
/// <param name="Accepted">Sempre verdadeiro quando retorna 202.</param>
/// <param name="EligibleTenants">Número de tenants elegíveis enfileirados.</param>
/// <param name="CorrelationId">Identificador de correlação desta execução (sem PII).</param>
public sealed record TriggerAcceptedResponse(
    bool Accepted,
    int EligibleTenants,
    Guid CorrelationId);
