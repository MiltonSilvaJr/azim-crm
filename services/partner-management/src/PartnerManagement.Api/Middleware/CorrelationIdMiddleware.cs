namespace PartnerManagement.Api.Middleware;

/// <summary>
/// Middleware que propaga o <c>correlation_id</c> entre requisições.
/// Lê o header <c>X-Correlation-Id</c> ou gera um novo identificador se ausente.
/// Escreve o <c>correlation_id</c> no header da resposta.
/// Injeta no <c>HttpContext.Items</c> para uso pelos controllers e behaviors.
/// Mapeia: TASK-23, RNF 5, design §5.4.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    /// <inheritdoc cref="IMiddleware.InvokeAsync"/>
    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
