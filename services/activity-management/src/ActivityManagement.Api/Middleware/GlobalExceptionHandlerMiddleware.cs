namespace ActivityManagement.Api.Middleware;

using System.Net;
using ActivityManagement.Application.Activities.Commands;
using ActivityManagement.Application.Behaviors;
using ActivityManagement.Contracts.Activities;
using ActivityManagement.Domain.Activities.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

/// <summary>
/// Middleware de tratamento global de exceções.
/// Mapeia exceções de domínio e de aplicação para o catálogo de erros ACT-ERR-001..011
/// no formato padronizado <see cref="ErrorResponse"/> (rule api-and-contracts.md, design §12).
/// Nunca expõe detalhes internos de infraestrutura nem PII nas mensagens de erro.
/// Mapeia: design §8, design §12, TASK-18.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Inicializa o middleware com o próximo delegate e o logger.</summary>
    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    /// <summary>Processa exceções e retorna resposta padronizada de erro.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items.TryGetValue("CorrelationId", out var corrObj)
                && corrObj is Guid corrId
                ? corrId
                : Guid.NewGuid();

            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception   exception,
        Guid        correlationId)
    {
        var (statusCode, code, message) = MapException(exception);

        // Log seguro — sem PII, sem detalhes de infra
        _logger.LogWarning(
            "Erro {Code} (HTTP {Status}) na requisição. CorrelationId={CorrelationId}",
            code, (int)statusCode, correlationId);

        context.Response.StatusCode  = (int)statusCode;
        context.Response.ContentType = "application/json";

        var error = new ErrorResponse(message, code, correlationId);
        await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
    }

    private static (HttpStatusCode Status, string Code, string Message) MapException(Exception ex) =>
        ex switch
        {
            // ── Domínio ───────────────────────────────────────────────────────
            TitleRequiredException            => (HttpStatusCode.BadRequest,  "ACT-ERR-001", "Título da atividade é obrigatório"),
            InvalidActivityTypeException      => (HttpStatusCode.BadRequest,  "ACT-ERR-002", "Tipo de atividade inválido"),
            ActivityTerminalException         => (HttpStatusCode.Conflict,    "ACT-ERR-011", "Operação inválida em atividade encerrada"),
            InvalidStatusTransitionException  => (HttpStatusCode.Conflict,    "ACT-ERR-004", "Transição de status inválida"),

            // ── Aplicação ─────────────────────────────────────────────────────
            ActivityNotFoundException         => (HttpStatusCode.NotFound,    "ACT-ERR-003", "Atividade não encontrada"),
            OpportunityNotFoundException      => (HttpStatusCode.UnprocessableEntity, "ACT-ERR-005", "Oportunidade vinculada não encontrada"),
            AccountNotFoundException          => (HttpStatusCode.UnprocessableEntity, "ACT-ERR-006", "Conta vinculada não encontrada"),
            InsufficientScopeException        => (HttpStatusCode.Forbidden,   "ACT-ERR-007", "Acesso negado"),
            InvalidDigestTokenException       => (HttpStatusCode.NotFound,    "ACT-ERR-008", "Link inválido"),
            ExpiredDigestTokenException       => (HttpStatusCode.Gone,        "ACT-ERR-009", "Link expirado"),
            TenantContextMissingException     => (HttpStatusCode.Unauthorized,"ACT-ERR-007", "Autenticação necessária"),

            // FluentValidation: título ausente ou due_at ausente
            FluentValidation.ValidationException fve => MapValidationException(fve),

            // ── Fallback ──────────────────────────────────────────────────────
            _                                 => (HttpStatusCode.InternalServerError, "ACT-ERR-000", "Erro interno do servidor"),
        };

    private static (HttpStatusCode, string, string) MapValidationException(
        FluentValidation.ValidationException ex)
    {
        var firstError = ex.Errors.FirstOrDefault();
        if (firstError?.PropertyName?.Contains("Title", StringComparison.OrdinalIgnoreCase) == true)
            return (HttpStatusCode.BadRequest, "ACT-ERR-001", "Título da atividade é obrigatório");

        if (firstError?.PropertyName?.Contains("DueAt", StringComparison.OrdinalIgnoreCase) == true)
            return (HttpStatusCode.BadRequest, "ACT-ERR-010", "Data de vencimento é obrigatória");

        if (firstError?.PropertyName?.Contains("Type", StringComparison.OrdinalIgnoreCase) == true)
            return (HttpStatusCode.BadRequest, "ACT-ERR-002", "Tipo de atividade inválido");

        return (HttpStatusCode.BadRequest, "ACT-ERR-001", firstError?.ErrorMessage ?? "Requisição inválida");
    }
}
