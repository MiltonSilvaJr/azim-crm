using System.Text.Json;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Contracts.ErrorCodes;
using OpportunityPipeline.Domain.Opportunities.Exceptions;
using ValidationException = OpportunityPipeline.Application.Common.ValidationException;

namespace OpportunityPipeline.Api.Middleware;

/// <summary>
/// Middleware de tratamento de exceções → ProblemDetails com OP-ERR-*.
/// Converte exceções de domínio e de aplicação em respostas HTTP padronizadas.
/// Nunca expõe informações de infraestrutura, PII ou dados de outro tenant.
/// Mapeia: design §8, §12, P10, TASK-20.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (ForbiddenException ex)
        {
            // HTTP 403 — RBAC negou a operação (OP-ERR-008 para reopen; genérico para outros)
            logger.LogWarning("Acesso negado para {Role} na operação {Path}.", ex.UserRole, context.Request.Path);
            await WriteProblemsAsync(context, 403, "Forbidden",
                ex.Message, ErrorCodes.OP_ERR_008, null).ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            // HTTP 422 — erro de validação com código OP-ERR-*
            var firstField = ex.Errors.Keys.FirstOrDefault();
            var firstMessage = firstField is not null && ex.Errors.TryGetValue(firstField, out var msgs)
                ? msgs.FirstOrDefault()
                : ex.Message;
            logger.LogInformation("Validação falhou: {Field} — {ErrorCode}.", firstField, ex.ErrorCode);
            await WriteProblemsAsync(context, 422, "Unprocessable Entity",
                firstMessage ?? ex.Message, ex.ErrorCode, firstField).ConfigureAwait(false);
        }
        catch (FluentValidation.ValidationException ex)
        {
            // HTTP 422 — FluentValidation (extrai primeiro OP-ERR da mensagem)
            var firstError = ex.Errors.FirstOrDefault();
            var errorCode = ExtractErrorCode(firstError?.ErrorMessage);
            logger.LogInformation("FluentValidation falhou: {Error}.", firstError?.ErrorMessage);
            await WriteProblemsAsync(context, 422, "Unprocessable Entity",
                firstError?.ErrorMessage ?? "Dados inválidos.", errorCode, firstError?.PropertyName).ConfigureAwait(false);
        }
        catch (InvalidStageTransitionException ex)
        {
            // HTTP 422 — transição de estágio inválida (OP-ERR-013)
            logger.LogInformation("Transição inválida: {Message}.", ex.Message);
            await WriteProblemsAsync(context, 422, "Unprocessable Entity",
                ex.Message, ErrorCodes.OP_ERR_013, null).ConfigureAwait(false);
        }
        catch (SnapshotImmutableException)
        {
            // HTTP 409 — snapshot imutável (não pode editar comissão após ganho)
            logger.LogInformation("Tentativa de modificar snapshot imutável.");
            await WriteProblemsAsync(context, 409, "Conflict",
                "Não é possível editar comissão após ganho — snapshot imutável.", null, null).ConfigureAwait(false);
        }
        catch (LossReasonRequiredException ex)
        {
            logger.LogInformation("Motivo de perda ausente: {Message}.", ex.Message);
            await WriteProblemsAsync(context, 422, "Unprocessable Entity",
                ex.Message, ErrorCodes.OP_ERR_006, "loss_reason_id").ConfigureAwait(false);
        }
        catch (OwnerRequiredException ex)
        {
            logger.LogInformation("Owner ausente: {Message}.", ex.Message);
            await WriteProblemsAsync(context, 422, "Unprocessable Entity",
                ex.Message, ErrorCodes.OP_ERR_002, "owner_id").ConfigureAwait(false);
        }
        catch (ExpectedCloseDateRequiredException ex)
        {
            logger.LogInformation("Data de fechamento ausente: {Message}.", ex.Message);
            await WriteProblemsAsync(context, 422, "Unprocessable Entity",
                ex.Message, ErrorCodes.OP_ERR_005, "expected_close_date").ConfigureAwait(false);
        }
        catch (PrimaryContactRequiredException ex)
        {
            logger.LogInformation("Contato principal ausente: {Message}.", ex.Message);
            await WriteProblemsAsync(context, 422, "Unprocessable Entity",
                ex.Message, ErrorCodes.OP_ERR_009, "contact_id").ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            // HTTP 401 — não autenticado
            logger.LogInformation("Acesso não autorizado a {Path}.", context.Request.Path);
            await WriteProblemsAsync(context, 401, "Unauthorized",
                "Autenticação necessária.", null, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // HTTP 500 — erro interno — nunca expõe detalhes de infraestrutura
            logger.LogError(ex, "Erro interno não tratado em {Path}.", context.Request.Path);
            await WriteProblemsAsync(context, 500, "Internal Server Error",
                "Erro interno. Tente novamente ou contate o suporte.", null, null).ConfigureAwait(false);
        }
    }

    private static async Task WriteProblemsAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        string? errorCode,
        string? field)
    {
        var correlationId = context.TraceIdentifier;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new OpProblemDetails
        {
            Title = title,
            Status = status,
            Detail = detail,
            ErrorCode = errorCode,
            CorrelationId = correlationId,
            Field = field
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions)).ConfigureAwait(false);
    }

    private static string? ExtractErrorCode(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        // Extrai padrão OP-ERR-NNN do início da mensagem
        var match = System.Text.RegularExpressions.Regex.Match(message, @"OP-ERR-\d{3}");
        return match.Success ? match.Value : null;
    }
}
