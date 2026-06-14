namespace ActivityManagement.Api.Middleware;

/// <summary>
/// Middleware que garante a presença do <c>X-Correlation-Id</c> em cada requisição.
/// Gera um novo UUID quando o header não está presente na entrada.
/// Expõe o valor via <c>HttpContext.Items["CorrelationId"]</c> para uso nos controllers
/// e via header <c>X-Correlation-Id</c> nas respostas (RNF 6.1, design §8).
/// Mapeia: RNF 6.1, TASK-18.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private const string ItemKey    = "CorrelationId";

    private readonly RequestDelegate _next;

    /// <summary>Inicializa o middleware com o próximo delegate da pipeline.</summary>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Processa a requisição injetando o correlation id.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
            && Guid.TryParse(value.ToString(), out var existingId)
            ? existingId
            : Guid.NewGuid();

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId.ToString();

        await _next(context);
    }
}
