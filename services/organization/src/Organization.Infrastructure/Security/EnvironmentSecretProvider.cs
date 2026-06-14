using Microsoft.Extensions.Configuration;
using Organization.Application.Ports;

namespace Organization.Infrastructure.Security;

/// <summary>
/// Implementação de <see cref="ISecretProvider"/> baseada em configuração da aplicação.
/// Em produção, deve ser substituída por <c>GcpSecretManagerProvider</c> que consulta
/// o GCP Secret Manager (design §10, RNF 3). Para o MVP/desenvolvimento, lê de
/// <c>IConfiguration</c> (suporta variáveis de ambiente, appsettings e Secret Manager local).
/// </summary>
public sealed class EnvironmentSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;

    /// <summary>Inicializa o provedor com a configuração da aplicação.</summary>
    public EnvironmentSecretProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public Task<string> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken = default)
    {
        // Busca na seção Secrets da configuração
        var value = _configuration[$"Secrets:{secretName}"]
            ?? _configuration[secretName]
            ?? throw new InvalidOperationException(
                $"Segredo '{secretName}' não encontrado na configuração. " +
                "Certifique-se de que a variável de ambiente ou o Secret Manager está configurado.");

        // Nunca logar o valor do segredo
        return Task.FromResult(value);
    }
}
