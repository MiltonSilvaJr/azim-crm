using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Google.Cloud.SecretManager.V1;
using Google.Api.Gax.ResourceNames;

namespace Authentication.Infrastructure.SecretManager;

/// <summary>
/// Adapter concreto de <see cref="ISecretProvider"/> que lê segredos em runtime
/// via GCP Secret Manager.
///
/// Garante que nenhum segredo seja armazenado em código, imagem, variável de
/// ambiente em texto claro, log ou erro. Falhas do Secret Manager são mapeadas
/// para <see cref="SecretProviderException"/> sem expor detalhe interno (RNF 7, DD-005).
///
/// A service account key do Firebase Admin SDK é obtida via este provider (DD-005).
///
/// Rotação sem redeploy: o Secret Manager suporta versões; o provider acessa
/// sempre a versão mais recente ("latest") a cada chamada, sem cache local
/// (intencional para suportar rotação sem reinicialização do serviço).
///
/// Mapeia: RNF 7, design.md § 6.6, § 6.8, DD-005, TASK-13.
/// </summary>
public sealed class SecretManagerProvider : ISecretProvider
{
    private readonly string _projectId;
    private readonly Func<string, CancellationToken, Task<string?>>? _clientOverride;

    /// <summary>
    /// Inicializa o provider para o projeto GCP informado.
    ///
    /// Usa credenciais Application Default Credentials (ADC) do ambiente em produção.
    /// </summary>
    /// <param name="projectId">ID do projeto GCP (ex.: "azim-prod").</param>
    /// <exception cref="ArgumentException">Quando <paramref name="projectId"/> for nulo ou vazio.</exception>
    public SecretManagerProvider(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("projectId não pode ser nulo ou vazio.", nameof(projectId));

        _projectId = projectId;
    }

    /// <summary>
    /// Construtor interno usado em testes para injetar um delegate de cliente substituível.
    /// </summary>
    private SecretManagerProvider(string projectId, Func<string, CancellationToken, Task<string?>> clientOverride)
        : this(projectId)
    {
        _clientOverride = clientOverride;
    }

    /// <summary>
    /// Cria uma instância com cliente falhante para uso exclusivo em testes.
    ///
    /// O delegate sempre lança <see cref="Exception"/> com a mensagem fornecida,
    /// simulando acesso negado ou serviço indisponível.
    /// </summary>
    /// <param name="projectId">ID do projeto GCP.</param>
    /// <param name="faultMessage">Mensagem da exceção simulada (interna — não exposta ao chamador).</param>
    public static SecretManagerProvider CreateWithFaultyClient(string projectId, string faultMessage)
    {
        return new SecretManagerProvider(
            projectId,
            (_, _) => throw new InvalidOperationException(faultMessage));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Acessa sempre a versão "latest" do segredo para suportar rotação sem redeploy.
    ///
    /// Qualquer exceção do Secret Manager é capturada e traduzida para
    /// <see cref="SecretProviderException"/> com mensagem genérica (DD-005, RNF 7.1).
    /// O nome do segredo e detalhes internos nunca são expostos ao chamador.
    /// </remarks>
    public async Task<string> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
            throw new ArgumentException("secretName não pode ser nulo ou vazio.", nameof(secretName));

        try
        {
            if (_clientOverride is not null)
            {
                // Caminho de teste: usa o delegate injetado.
                var overrideResult = await _clientOverride(secretName, cancellationToken);
                return overrideResult
                    ?? throw new SecretProviderException("Serviço de configuração segura indisponível.");
            }

            return await AccessSecretVersionAsync(secretName, cancellationToken);
        }
        catch (SecretProviderException)
        {
            // Relança sem encapsular: já é o tipo correto.
            throw;
        }
        catch (Exception ex)
        {
            // Captura qualquer exceção do SDK ou de rede e mapeia para tipo controlado.
            // Nunca expõe secretName, detalhes do SDK ou stack trace ao chamador (DD-005, RNF 7.1).
            throw new SecretProviderException(
                "Serviço de configuração segura indisponível.",
                ex);
        }
    }

    /// <summary>
    /// Acessa o GCP Secret Manager via SDK e retorna o valor da versão mais recente.
    ///
    /// Criação do cliente usa ADC (Application Default Credentials).
    /// </summary>
    private async Task<string> AccessSecretVersionAsync(
        string secretName,
        CancellationToken cancellationToken)
    {
        var client = await SecretManagerServiceClient.CreateAsync(cancellationToken);

        var versionName = new SecretVersionName(_projectId, secretName, "latest");
        var response = await client.AccessSecretVersionAsync(versionName, cancellationToken);

        var payload = response.Payload?.Data?.ToStringUtf8();

        if (string.IsNullOrEmpty(payload))
            throw new SecretProviderException("Serviço de configuração segura indisponível.");

        return payload;
    }
}
