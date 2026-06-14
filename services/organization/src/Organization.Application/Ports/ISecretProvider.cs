namespace Organization.Application.Ports;

/// <summary>
/// Port de acesso a segredos externos (Secret Manager, variáveis de ambiente, etc.).
/// Implementado na Infrastructure para isolar a dependência do provedor real.
/// Sem PII nos parâmetros — apenas nomes de chave técnicos.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Retorna o valor de um segredo pelo nome da chave.
    /// </summary>
    /// <param name="secretName">Nome da chave do segredo (sem espaços, snake_case).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Valor do segredo em texto claro; nunca logar este valor.</returns>
    /// <exception cref="InvalidOperationException">Quando o segredo não é encontrado.</exception>
    Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}
