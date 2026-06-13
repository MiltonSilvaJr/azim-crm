namespace Authentication.Application.Ports;

/// <summary>
/// Porta de saída que abstrai o acesso a segredos em runtime via o gerenciador de segredos.
///
/// Implementada por <c>SecretManagerProvider</c> em Infrastructure, que acessa o
/// GCP Secret Manager (RNF 7, DD-005). Nenhum segredo é armazenado em código,
/// imagem, variável de ambiente em texto claro, log ou erro.
///
/// Mapeia: RNF 7, design.md § 6.6, § 6.8, DD-005.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Obtém o valor de um segredo pelo nome.
    ///
    /// Nunca lança para segredo não encontrado sem configuração: lança
    /// <see cref="SecretProviderException"/> com detalhe interno não exposto ao chamador.
    /// </summary>
    /// <param name="secretName">Nome do segredo no gerenciador (ex.: "firebase-service-account-key").</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Valor do segredo (nunca nulo).</returns>
    /// <exception cref="Exceptions.SecretProviderException">
    /// Lançada quando o segredo não existe, o acesso é negado ou o gerenciador está indisponível.
    /// </exception>
    Task<string> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken = default);
}
