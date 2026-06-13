using NotificationDelivery.Application.Ports;

namespace NotificationDelivery.Infrastructure.Tests.Secrets;

/// <summary>
/// Fake de <see cref="ISecretProvider"/> para uso em testes de Infrastructure.
///
/// Permite controlar exatamente quando retornar um valor ou lançar exceção,
/// sem depender do GCP Secret Manager real em CI (RISK-EXEC-03).
///
/// Registra quantas chamadas foram feitas para verificar o comportamento do cache (ST-02 da TASK-14).
/// </summary>
public sealed class SecretProviderFake : ISecretProvider
{
    private readonly Dictionary<string, string> _secrets;
    private readonly Dictionary<string, bool> _failOn;
    private readonly Dictionary<string, int> _callCount = [];

    /// <summary>
    /// Constrói o fake com os segredos pré-configurados.
    /// </summary>
    /// <param name="secrets">Mapeamento nome → valor de segredo.</param>
    /// <param name="failOn">Nomes de segredo que devem lançar <see cref="SecretProviderException"/>.</param>
    public SecretProviderFake(
        Dictionary<string, string>? secrets = null,
        Dictionary<string, bool>? failOn = null)
    {
        _secrets = secrets ?? [];
        _failOn = failOn ?? [];
    }

    /// <summary>Número de chamadas feitas para o segredo especificado.</summary>
    public int GetCallCount(string secretName) =>
        _callCount.TryGetValue(secretName, out var count) ? count : 0;

    /// <inheritdoc/>
    public Task<string> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken = default)
    {
        // Incrementa contador de chamadas
        _callCount[secretName] = GetCallCount(secretName) + 1;

        // Simula falha se configurado
        if (_failOn.TryGetValue(secretName, out var fail) && fail)
        {
            throw new SecretProviderException(
                secretName,
                $"Falha simulada ao recuperar segredo '{secretName}'. (NOTIF-ERR-040)");
        }

        // Retorna o valor configurado ou lança KeyNotFoundException
        if (_secrets.TryGetValue(secretName, out var value))
            return Task.FromResult(value);

        throw new SecretProviderException(
            secretName,
            $"Segredo '{secretName}' não encontrado. (NOTIF-ERR-040)");
    }
}
