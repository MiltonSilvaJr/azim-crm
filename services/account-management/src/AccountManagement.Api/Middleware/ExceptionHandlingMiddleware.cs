using AccountManagement.Application.Exceptions;
using AccountManagement.Contracts.Common;
using AccountManagement.Domain.Accounts.Exceptions;
using FluentValidation;
using System.Net;
using System.Text.Json;

namespace AccountManagement.Api.Middleware;

/// <summary>
/// Middleware de tratamento centralizado de exceções.
///
/// Mapeia exceções do domínio e da aplicação para respostas HTTP com o formato
/// padronizado <c>{ "error", "code", "correlationId" }</c> (design §12, rule api-and-contracts.md).
///
/// Nunca expõe PII, stack trace ou detalhes internos de infraestrutura (RNF 1.3, design §12).
///
/// Catálogo ACC-ERR coberto:
/// - ACC-ERR-001 — AccountNameRequiredException (400)
/// - ACC-ERR-002 — ValidationException com código ACC-ERR-002 (400)
/// - ACC-ERR-003 — AccountNotFoundException (404)
/// - ACC-ERR-004 — InvalidEmailException (400)
/// - ACC-ERR-005 — ContactName required (400)
/// - ACC-ERR-006 — ContactNotFoundException (404)
/// - ACC-ERR-007 — ContactAlreadyForgottenException (409)
/// - ACC-ERR-008 — PiiAccessDeniedException (403)
/// - ACC-ERR-009 — IdempotencyConflictException (409)
///
/// Mapeia: TASK-13 (ST-02), TASK-14 (ST-02), design §12, ACC-ERR-001..009.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>Inicializa o middleware.</summary>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Executa o middleware capturando exceções e convertendo para resposta padronizada.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Response.Headers.TryGetValue(CorrelationIdHeader, out var existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString();

        var (statusCode, code, message) = MapException(exception);

        // Log sem PII — apenas código de erro e correlation_id (RNF 1.1)
        _logger.LogWarning(
            "Erro tratado: code={Code} status={Status} correlationId={CorrelationId}",
            code, statusCode, correlationId);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse(
            Error: message,
            Code: code,
            CorrelationId: correlationId);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        await context.Response.WriteAsync(json);
    }

    private static (HttpStatusCode StatusCode, string Code, string Message) MapException(
        Exception exception) => exception switch
    {
        // Validação FluentValidation com código explícito
        ValidationException valEx when GetFirstErrorCode(valEx) == "ACC-ERR-001"
            => (HttpStatusCode.BadRequest, "ACC-ERR-001", "Nome da conta é obrigatório."),

        ValidationException valEx when GetFirstErrorCode(valEx) == "ACC-ERR-002"
            => (HttpStatusCode.BadRequest, "ACC-ERR-002", "Parâmetros de busca inválidos."),

        ValidationException valEx when GetFirstErrorCode(valEx) == "ACC-ERR-004"
            => (HttpStatusCode.BadRequest, "ACC-ERR-004", "E-mail do contato é inválido."),

        ValidationException valEx when GetFirstErrorCode(valEx) == "ACC-ERR-005"
            => (HttpStatusCode.BadRequest, "ACC-ERR-005", "Nome do contato é obrigatório."),

        ValidationException valEx when GetFirstErrorCode(valEx) == "ACC-ERR-009"
            => (HttpStatusCode.Conflict, "ACC-ERR-009", "Requisição duplicada."),

        ValidationException
            => (HttpStatusCode.BadRequest, "ACC-ERR-001", "Dados inválidos na requisição."),

        // Domínio — exceções de entidade
        AccountNameRequiredException
            => (HttpStatusCode.BadRequest, "ACC-ERR-001", "Nome da conta é obrigatório."),

        InvalidEmailException
            => (HttpStatusCode.BadRequest, "ACC-ERR-004", "E-mail do contato é inválido."),

        AccountNotFoundException
            => (HttpStatusCode.NotFound, "ACC-ERR-003", "Conta não encontrada."),

        ContactNotFoundException
            => (HttpStatusCode.NotFound, "ACC-ERR-006", "Contato não encontrado."),

        ContactAlreadyForgottenException
            => (HttpStatusCode.Conflict, "ACC-ERR-007", "Contato já anonimizado."),

        // Aplicação
        PiiAccessDeniedException
            => (HttpStatusCode.Forbidden, "ACC-ERR-008", "Acesso negado."),

        IdempotencyConflictException
            => (HttpStatusCode.Conflict, "ACC-ERR-009", "Requisição duplicada."),

        // Qualquer outra exceção não mapeada — 500 genérico sem detalhe
        _
            => (HttpStatusCode.InternalServerError, "ACC-ERR-000", "Erro interno do servidor."),
    };

    private static string GetFirstErrorCode(ValidationException ex) =>
        ex.Errors.FirstOrDefault()?.ErrorCode ?? string.Empty;
}
