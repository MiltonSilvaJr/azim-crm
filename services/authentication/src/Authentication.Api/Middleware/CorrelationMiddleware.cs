namespace Authentication.Api.Middleware;

/// <summary>
/// Middleware que garante que 100% das requisições contenham um <c>correlationId</c>.
///
/// Comportamento:
///   - Lê o header <c>X-Correlation-ID</c> da requisição. Se presente, propaga.
///   - Se ausente, gera um novo UUID e injeta.
///   - Adiciona o <c>correlationId</c> ao header de resposta.
///   - Adiciona ao escopo de log via <see cref="ILogger"/> (RNF 4.1).
///
/// É o primeiro middleware do pipeline (design.md § 5.4, ordem obrigatória).
///
/// Mapeia: TASK-15, design.md § 5.4, RNF 4.1, Req 1.
/// </summary>
public sealed class CorrelationMiddleware
{
    /// <summary>Nome do header HTTP de correlação.</summary>
    public const string CorrelationIdHeader = "X-Correlation-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    /// <summary>
    /// Inicializa o middleware de correlação.
    /// </summary>
    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Garante <c>correlationId</c> na requisição e na resposta.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Ler ou gerar correlationId
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existing)
                            && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString();

        // Injetar no HttpContext para acesso por outros middlewares
        context.Items[CorrelationIdHeader] = correlationId;

        // Adicionar ao header de resposta antes de continuar
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
                context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Adicionar ao escopo de log (RNF 4.1)
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = correlationId
        });

        await _next(context);
    }
}
