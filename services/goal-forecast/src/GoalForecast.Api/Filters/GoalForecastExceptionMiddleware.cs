using System.Text.Json;
using FluentValidation;
using GoalForecast.Application.Common;
using GoalForecast.Domain.Exceptions;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Api.Filters;

/// <summary>
/// Middleware de tratamento de exceções do módulo goal-forecast.
/// Converte exceções de domínio e de aplicação em ProblemDetails com código GF-ERR-* (design §12).
///
/// Garante que mensagens de erro não expõem detalhes internos de infraestrutura (RNF-2.3).
///
/// Mapeia: design §12, catálogo de erros GF-ERR-001..008, TASK-22.
/// </summary>
public sealed class GoalForecastExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GoalForecastExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GoalForecastExceptionMiddleware(
        RequestDelegate next,
        ILogger<GoalForecastExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteErrorResponse(context, ex.SuggestedHttpStatus, ex.ErrorCode, ex.Message);
        }
        catch (DomainException ex)
        {
            // DomainException mapeia para 400 por padrão (validação de domínio)
            var httpStatus = MapDomainErrorCode(ex.ErrorCode);
            await WriteErrorResponse(context, httpStatus, ex.ErrorCode, ex.Message);
        }
        catch (ValidationException ex)
        {
            // FluentValidation.ValidationException → 400 com o primeiro erro como código
            var firstError = ex.Errors.FirstOrDefault();
            var errorCode = firstError?.ErrorCode ?? "GF-ERR-VAL";
            var detail = firstError?.ErrorMessage ?? ex.Message;
            await WriteErrorResponse(context, 400, errorCode, detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GoalForecastExceptionMiddleware: exceção não tratada. Path={Path}",
                context.Request.Path);

            await WriteErrorResponse(context, 500, "GF-ERR-INTERNAL",
                "Erro interno. Tente novamente.");
        }
    }

    private static int MapDomainErrorCode(string code) => code switch
    {
        "GF-ERR-001" => 400,
        "GF-ERR-002" => 400,
        "GF-ERR-003" => 400,
        "GF-ERR-004" => 422,
        "GF-ERR-005" => 409,
        "GF-ERR-006" => 403,
        "GF-ERR-007" => 404,
        _ => 400
    };

    private static async Task WriteErrorResponse(
        HttpContext context,
        int statusCode,
        string errorCode,
        string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://azim.com/errors/{errorCode.ToLowerInvariant()}",
            title = errorCode,
            status = statusCode,
            detail,
            errorCode
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions));
    }
}
