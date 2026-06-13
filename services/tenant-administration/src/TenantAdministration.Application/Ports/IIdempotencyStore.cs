namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de saída para armazenamento de chaves de idempotência.
/// Usado pelo <c>IdempotencyBehavior</c> para evitar reexecução de commands já processados.
/// Implementação concreta vem na camada de Infrastructure (Onda 4).
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Verifica se a chave de idempotência já foi processada e retorna o resultado
    /// serializado anteriormente. Retorna <c>null</c> se a chave ainda não existe.
    /// </summary>
    /// <param name="key">Chave de idempotência (ex.: valor do header <c>Idempotency-Key</c>).</param>
    /// <param name="ct">Token de cancelamento.</param>
    ValueTask<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Armazena o resultado de uma execução associado à chave de idempotência.
    /// </summary>
    /// <param name="key">Chave de idempotência.</param>
    /// <param name="resultJson">Resultado serializado em JSON.</param>
    /// <param name="ct">Token de cancelamento.</param>
    ValueTask SetAsync(string key, string resultJson, CancellationToken ct = default);
}
