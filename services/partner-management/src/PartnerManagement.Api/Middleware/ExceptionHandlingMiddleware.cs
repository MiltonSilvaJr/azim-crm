using System.Text.Json;
using PartnerManagement.Application.Behaviors;
using PartnerManagement.Application.Partners;
using PartnerManagement.Domain.Partners.Exceptions;

namespace PartnerManagement.Api.Middleware;

/// <summary>
/// Middleware centralizado de tratamento de exceções.
/// Converte exceções de domínio e de aplicação em respostas HTTP padronizadas
/// no formato <c>{ "error", "code", "correlationId" }</c> (design §8, design §12).
/// Nunca expõe dados de contato (<c>contact_email</c>/<c>contact_phone</c>) nas mensagens de erro (RNF 4).
/// <c>partner.name</c> não é PII por VAL-PARTNER-01 (2026-06-15).
/// Mapeia: TASK-23, design §12, catálogo PM-ERR-001..011.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc cref="IMiddleware.InvokeAsync"/>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        string correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out object? val)
            ? val?.ToString() ?? Guid.NewGuid().ToString("N")
            : Guid.NewGuid().ToString("N");

        (int statusCode, string errorCode, string message) = exception switch
        {
            PartnerNameRequiredException => (400, PartnerErrors.NameRequired, "Nome do parceiro é obrigatório."),
            InvalidPartnerRoleException => (400, PartnerErrors.InvalidRole, "Papel de parceiro inválido."),
            PercentageOutOfRangeException => (400, PartnerErrors.PercentageOutOfRange, "Percentual fora do intervalo permitido."),
            InvalidPartnerContactException => (400, PartnerErrors.InvalidContactEmail, "E-mail de contato inválido."),
            PartnerNotFoundException => (404, PartnerErrors.PartnerNotFound, "Parceiro não encontrado."),
            AccessDeniedException => (403, PartnerErrors.AccessDenied, "Acesso negado."),
            InvalidCommissionPeriodException => (400, PartnerErrors.InvalidCommissionPeriod, "Período de comissão inválido."),
            DuplicateIdempotencyKeyException => (409, PartnerErrors.DuplicateRequest, "Requisição duplicada."),
            _ => (500, "INTERNAL_ERROR", "Erro interno do servidor.")
        };

        // Logar sem PII — apenas código de erro e correlationId
        if (statusCode >= 500)
        {
            logger.LogError(
                exception,
                "Erro não tratado. CorrelationId={CorrelationId} Code={Code}",
                correlationId,
                errorCode);
        }
        else
        {
            logger.LogWarning(
                "Erro de domínio/aplicação. CorrelationId={CorrelationId} Code={Code}",
                correlationId,
                errorCode);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = message,
            code = errorCode,
            correlationId
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(errorResponse, JsonOptions));
    }
}

/// <summary>
/// Exceção lançada quando uma <c>Idempotency-Key</c> é reutilizada com payload divergente (PM-ERR-010).
/// </summary>
public sealed class DuplicateIdempotencyKeyException(string idempotencyKey)
    : Exception($"Requisição duplicada para chave de idempotência: {idempotencyKey}.")
{
    /// <summary>Chave de idempotência duplicada.</summary>
    public string IdempotencyKey { get; } = idempotencyKey;
}
