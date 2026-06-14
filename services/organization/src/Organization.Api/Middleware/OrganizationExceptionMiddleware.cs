using Microsoft.AspNetCore.Mvc;
using Organization.Domain.Exceptions;
using System.Text.Json;

namespace Organization.Api.Middleware;

/// <summary>
/// Middleware global de tratamento de exceções.
/// Mapeia exceções conhecidas para Problem Details (RFC 7807) com <c>code</c> do catálogo ORG-ERR.
/// Sem PII em mensagens de erro; sem stack trace em produção.
/// </summary>
public sealed class OrganizationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OrganizationExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>Inicializa o middleware.</summary>
    public OrganizationExceptionMiddleware(
        RequestDelegate next,
        ILogger<OrganizationExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    /// <inheritdoc/>
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
        var (statusCode, title, detail, code) = MapException(exception);

        _logger.LogWarning(
            exception,
            "Exceção tratada: {ExceptionType} — status={StatusCode} code={ErrorCode}",
            exception.GetType().Name,
            statusCode,
            code);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problem.Extensions["code"] = code;

        // Sem stack trace em produção
        if (_env.IsDevelopment())
            problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            JsonOptions,
            context.RequestAborted);
    }

    private static (int StatusCode, string Title, string Detail, string Code) MapException(Exception exception)
    {
        return exception switch
        {
            // Mapeamentos do catálogo ORG-ERR-001..017
            DomainException domainEx => MapDomainException(domainEx),

            // RBAC — deny-by-default
            UnauthorizedAccessException => (
                403,
                "Você não tem permissão para esta ação.",
                exception.Message.Contains("ORG-ERR-008")
                    ? exception.Message
                    : "Você não tem permissão para esta ação.",
                ExtractOrgErrCode(exception.Message, "ORG-ERR-010")),

            // 401 — autenticação ausente (contexto de tenant vazio)
            InvalidOperationException ex when ex.Message.Contains("tenant_id")
                                           || ex.Message.Contains("Contexto de tenant") => (
                401,
                "Não autenticado.",
                "Autenticação necessária.",
                "ORG-ERR-AUTH"),

            // InvalidOperationException com código ORG-ERR conhecido → mapear para status correto
            InvalidOperationException ex when ExtractOrgErrCode(ex.Message, string.Empty) is { Length: > 0 } code
                => MapInvalidOperationToOrgErr(code, ex.Message),

            // Recurso não encontrado
            KeyNotFoundException => (
                404,
                "Recurso não encontrado.",
                "O item solicitado não foi encontrado.",
                "ORG-ERR-016"),

            // Validação FluentValidation
            FluentValidation.ValidationException vex => (
                400,
                "Requisição inválida.",
                string.Join("; ", vex.Errors.Select(e => e.ErrorMessage)),
                "ORG-ERR-VALIDATION"),

            // Fallback genérico — sem expor detalhes internos
            _ => (500, "Erro interno.", "Um erro inesperado ocorreu.", "ORG-ERR-INTERNAL"),
        };
    }

    private static (int StatusCode, string Title, string Detail, string Code) MapInvalidOperationToOrgErr(
        string code,
        string message)
    {
        return code switch
        {
            "ORG-ERR-001" => (409, "Nome duplicado.", "Já existe uma BU com este nome.", code),
            "ORG-ERR-002" => (409, "BU possui oportunidades ativas.", message, code),
            "ORG-ERR-003" => (409, "Não foi possível concluir o convite.", "Não foi possível concluir o convite.", code),
            "ORG-ERR-004" => (400, "Convite inválido ou expirado.", "Convite inválido ou expirado.", code),
            "ORG-ERR-005" => (409, "Convite já utilizado.", "Convite já utilizado.", code),
            "ORG-ERR-009" => (409, "Último administrador ativo.", "O tenant precisa ter ao menos um administrador ativo.", code),
            "ORG-ERR-011" => (409, "Usuário com atividades futuras.", message, code),
            _ => (422, "Regra de negócio violada.", message, code),
        };
    }

    private static (int StatusCode, string Title, string Detail, string Code) MapDomainException(DomainException ex)
    {
        // Detecta código ORG-ERR-NNN na mensagem e mapeia para HTTP status
        var code = ExtractOrgErrCode(ex.Message, "ORG-ERR-DOMAIN");

        return code switch
        {
            "ORG-ERR-001" => (409, "Nome duplicado.", ex.Message, code),
            "ORG-ERR-002" => (409, "BU possui oportunidades ativas.", ex.Message, code),
            "ORG-ERR-003" => (409, "Não foi possível concluir o convite.", "Não foi possível concluir o convite.", code),
            "ORG-ERR-004" => (400, "Convite inválido ou expirado.", "Convite inválido ou expirado.", code),
            "ORG-ERR-005" => (409, "Convite já utilizado.", "Convite já utilizado.", code),
            "ORG-ERR-006" => (409, "Convite não pode ser revogado neste estado.", ex.Message, code),
            "ORG-ERR-007" => (400, "Papel inválido.", "Papel inválido para o vínculo.", code),
            "ORG-ERR-008" => (403, "Permissão insuficiente.", "Apenas o administrador pode realizar esta ação na BU.", code),
            "ORG-ERR-009" => (409, "Último administrador ativo.", "O tenant precisa ter ao menos um administrador ativo.", code),
            "ORG-ERR-011" => (409, "Usuário com atividades futuras.", ex.Message, code),
            "ORG-ERR-012" => (409, "Membership duplicado.", "Usuário já possui vínculo nesta BU.", code),
            "ORG-ERR-013" => (409, "Nome de estágio duplicado.", "Já existe um estágio com este nome nesta BU.", code),
            "ORG-ERR-014" => (400, "Posição inválida.", "Posição de estágio inválida ou duplicada.", code),
            "ORG-ERR-015" => (409, "Estágio terminal.", "Não é possível remover o último estágio terminal.", code),
            "ORG-ERR-016" => (404, "Item não encontrado.", "Item de configuração não encontrado.", code),
            "ORG-ERR-017" => (409, "Último motivo de perda.", "A BU precisa de ao menos um motivo de perda ativo.", code),
            _ => (422, "Regra de domínio violada.", ex.Message, code),
        };
    }

    private static string ExtractOrgErrCode(string message, string fallback)
    {
        // Detecta padrão ORG-ERR-NNN na mensagem
        var match = System.Text.RegularExpressions.Regex.Match(
            message ?? string.Empty,
            @"ORG-ERR-\d+");

        return match.Success ? match.Value : fallback;
    }
}

/// <summary>
/// Extensões para registrar o middleware de exceções no pipeline HTTP.
/// </summary>
public static class ExceptionMiddlewareExtensions
{
    /// <summary>Adiciona o <see cref="OrganizationExceptionMiddleware"/> ao pipeline.</summary>
    public static IApplicationBuilder UseOrganizationExceptionHandler(
        this IApplicationBuilder app)
    {
        app.UseMiddleware<OrganizationExceptionMiddleware>();
        return app;
    }
}
