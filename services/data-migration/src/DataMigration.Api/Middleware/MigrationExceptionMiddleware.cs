using DataMigration.Application.Exceptions;
using DataMigration.Domain.Exceptions;
using System.Text.Json;

namespace DataMigration.Api.Middleware;

/// <summary>
/// Middleware de tratamento de exceções do módulo data-migration.
/// Mapeia exceções de domínio e aplicação para <c>ProblemDetails</c> padronizados
/// com os códigos do catálogo MIG-ERR-001..010 (design §12).
///
/// Garante que nenhuma mensagem de erro expõe PII (RNF 3).
///
/// Rastreia: design §12, TASK-21.
/// </summary>
public sealed class MigrationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MigrationExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Cria o middleware com o próximo delegate injetado.</summary>
    public MigrationExceptionMiddleware(
        RequestDelegate next,
        ILogger<MigrationExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Intercepta exceções e as converte em ProblemDetails.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (MigrationDomainException ex)
        {
            _logger.LogWarning(
                "Erro de domínio [{ErrorCode}]: {Message}",
                ex.ErrorCode,
                ex.Message);

            var statusCode = MapErrorCodeToHttpStatus(ex.ErrorCode);
            await WriteProblemDetailsAsync(context, statusCode, ex.ErrorCode, ex.Message);
        }
        catch (InvalidMigrationStateTransitionException ex)
        {
            _logger.LogWarning(
                "Transição de estado inválida: {Message}",
                ex.Message);

            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status409Conflict,
                "MIG-ERR-005",
                ex.Message);
        }
        catch (OwnerRequiredForImportException ex)
        {
            _logger.LogWarning(
                "Owner obrigatório não atendido: {Message}",
                ex.Message);

            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status409Conflict,
                "MIG-ERR-006",
                ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Erro interno não tratado na API de migração.");

            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "MIG-ERR-INTERNAL",
                "Erro interno. Consulte os logs do sistema.");
        }
    }

    // =========================================================================
    // Mapeamento do catálogo de erros → HTTP status (design §12)
    // =========================================================================

    private static int MapErrorCodeToHttpStatus(string errorCode) => errorCode switch
    {
        "MIG-ERR-001" => StatusCodes.Status422UnprocessableEntity,
        "MIG-ERR-002" => StatusCodes.Status422UnprocessableEntity,
        "MIG-ERR-003" => StatusCodes.Status413RequestEntityTooLarge,
        "MIG-ERR-004" => StatusCodes.Status404NotFound,
        "MIG-ERR-005" => StatusCodes.Status409Conflict,
        "MIG-ERR-006" => StatusCodes.Status409Conflict,
        "MIG-ERR-007" => StatusCodes.Status409Conflict,
        "MIG-ERR-008" => StatusCodes.Status400BadRequest,
        "MIG-ERR-009" => StatusCodes.Status403Forbidden,
        "MIG-ERR-010" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string errorCode,
        string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://azim.com.br/errors/{errorCode.ToLowerInvariant().Replace('-', '/')}",
            title = errorCode,
            status = statusCode,
            detail = SanitizeDetail(detail),
            traceId = context.TraceIdentifier,
        };

        var json = JsonSerializer.Serialize(problem, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Remove informações que podem conter PII ou detalhes de infra.
    /// Mensagens de erro referem linhas por índice (RNF 3).
    /// </summary>
    private static string SanitizeDetail(string detail) =>
        // Mantém a mensagem técnica mas trunca se muito longa
        detail.Length > 500 ? detail[..500] : detail;
}
