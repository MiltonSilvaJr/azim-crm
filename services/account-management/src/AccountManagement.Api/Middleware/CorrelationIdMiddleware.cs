using AccountManagement.Application.Behaviors;

namespace AccountManagement.Api.Middleware;

/// <summary>
/// Middleware que garante a existência de <c>X-Correlation-Id</c> em toda requisição.
///
/// Se o cabeçalho vier do cliente, é preservado; caso contrário, um novo UUID é gerado.
/// O valor é adicionado ao cabeçalho de resposta e injetado no <see cref="CorrelationContext"/>
/// para uso pelos behaviors de logging (design §5.4, RNF 9).
///
/// Mapeia: TASK-13 (ST-02), design §11, RNF 9.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    /// <summary>Inicializa o middleware com o próximo delegate do pipeline.</summary>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Executa o middleware injetando o correlation_id no contexto.</summary>
    public async Task InvokeAsync(HttpContext context, CorrelationContext correlationContext)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString();

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Tenta extrair tenant_id do contexto de autenticação para o CorrelationContext
        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            correlationContext.SetCorrelation(correlationId, tenantId);
        }

        await _next(context);
    }
}
