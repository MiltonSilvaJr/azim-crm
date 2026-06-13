namespace Authentication.Application.Ports;

/// <summary>
/// Porta de saída que abstrai o controle de taxa de requisições por IP e por tenant.
///
/// Implementado pelo adapter em Infrastructure (ex.: Redis sliding window).
/// Excesso retorna <see langword="false"/> — o serviço de API responde 429 AUTH-ERR-040
/// com mensagem genérica que não revela existência de conta (RNF 8.2).
///
/// Mapeia: RNF 8, design.md § 6.4, Req 10.5.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Verifica se a requisição está dentro do limite permitido e incrementa o contador.
    ///
    /// Aplica janela deslizante por chave composta (<paramref name="partitionKey"/>).
    /// Retorna <see langword="false"/> quando o limite foi excedido.
    /// </summary>
    /// <param name="partitionKey">
    /// Chave de particionamento (ex.: "ip:{ip_address}" ou "tenant:{tenant_id}:{ip}").
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// <see langword="true"/> quando dentro do limite; <see langword="false"/> quando excedido.
    /// </returns>
    Task<bool> IsAllowedAsync(
        string partitionKey,
        CancellationToken cancellationToken = default);
}
