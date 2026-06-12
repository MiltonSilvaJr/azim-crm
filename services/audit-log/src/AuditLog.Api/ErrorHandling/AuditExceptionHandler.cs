using AuditLog.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AuditLog.Api.ErrorHandling;

/// <summary>
/// Handler de exceções para o módulo de auditoria.
/// Traduz exceções de domínio/aplicação para <see cref="ProblemDetails"/> com campo <c>errorCode</c>
/// e status HTTP correto (design §12, AUD-ERR-001..008).
/// <para>
/// Regras:
/// <list type="bullet">
/// <item><description>Mensagens nunca expõem PII, <c>delta_json</c> nem stack trace (RNF-002.3).</description></item>
/// <item><description>AUD-ERR-002 e AUD-ERR-004 têm título idêntico (anti-enumeração, REQ-005.3).</description></item>
/// <item><description>Exceções desconhecidas retornam 500 com mensagem genérica.</description></item>
/// </list>
/// </para>
/// </summary>
internal sealed class AuditExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AuditExceptionHandler> _logger;

    public AuditExceptionHandler(ILogger<AuditExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is AuditApplicationException auditEx)
        {
            return await HandleAuditExceptionAsync(httpContext, auditEx, cancellationToken);
        }

        // Exceção genérica não tratada — log interno sem PII, resposta genérica
        _logger.LogError(
            exception,
            "Exceção não tratada no módulo de auditoria. Path={Path}",
            httpContext.Request.Path);

        var fallback = CreateProblemDetails(
            httpContext,
            statusCode: 500,
            title: "Erro interno ao processar a requisição.",
            errorCode: AuditErrorCodes.PersistenceFailure);

        return await WriteProblemDetailsAsync(httpContext, fallback, cancellationToken);
    }

    // ------------------------------------------------------------------ Tratamento de AuditApplicationException

    private async ValueTask<bool> HandleAuditExceptionAsync(
        HttpContext httpContext,
        AuditApplicationException exception,
        CancellationToken cancellationToken)
    {
        if (!AuditErrors.Catalog.TryGetValue(exception.ErrorCode, out var entry))
        {
            // Código desconhecido — trata como 500 genérico
            _logger.LogWarning(
                "Código de erro desconhecido no catálogo: {ErrorCode}",
                exception.ErrorCode);
            entry = (500, "Erro ao processar a requisição.");
        }

        var (statusCode, title) = entry;

        _logger.LogWarning(
            "Exceção de auditoria tratada: ErrorCode={ErrorCode} Status={Status}",
            exception.ErrorCode,
            statusCode);

        var problem = CreateProblemDetails(httpContext, statusCode, title, exception.ErrorCode);
        return await WriteProblemDetailsAsync(httpContext, problem, cancellationToken);
    }

    // ------------------------------------------------------------------ Helpers

    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int statusCode,
        string title,
        string errorCode)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["errorCode"] = errorCode;

        // correlationId para rastreabilidade (RNF-002.1)
        if (httpContext.TraceIdentifier is { } traceId)
            problem.Extensions["correlationId"] = traceId;

        return problem;
    }

    private static async ValueTask<bool> WriteProblemDetailsAsync(
        HttpContext httpContext,
        ProblemDetails problem,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = problem.Status ?? 500;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
