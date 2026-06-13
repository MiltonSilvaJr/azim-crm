using Google.Cloud.SecretManager.V1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationDelivery.Application.Ports;

namespace NotificationDelivery.Infrastructure.Secrets;

/// <summary>
/// Implementação de <see cref="ISecretProvider"/> que lê segredos do GCP Secret Manager
/// com cache em memória de TTL curto (DD-007, Req 10, RNF 6, design §6.7).
///
/// <para>Invariantes de segurança (RNF 6, DD-007):</para>
/// <list type="bullet">
///   <item><description>O valor do segredo NUNCA é gravado em logs (RNF-6.3). Apenas o nome lógico aparece.</description></item>
///   <item><description>Cache em memória com TTL configurável evita chamadas repetidas ao Secret Manager (design §6.2).</description></item>
///   <item><description>Falha de acesso lança <see cref="SecretProviderException"/> com mensagem sem credencial.</description></item>
///   <item><description>SDK do GCP Secret Manager confinado nesta classe — não vaza para Application/Contracts (RNF 1, DD-003).</description></item>
/// </list>
/// </summary>
public sealed class SecretManagerProvider : ISecretProvider, IDisposable
{
    private readonly SecretManagerServiceClient _client;
    private readonly IMemoryCache _cache;
    private readonly SecretManagerOptions _options;
    private readonly ILogger<SecretManagerProvider> _logger;
    private bool _disposed;

    /// <summary>
    /// Constrói o provider com cliente GCP Secret Manager, cache e opções injetados.
    /// </summary>
    /// <param name="client">Cliente do GCP Secret Manager (pode ser mockado nos testes).</param>
    /// <param name="cache">Cache em memória para TTL curto.</param>
    /// <param name="options">Opções de configuração (TTL, ProjectId).</param>
    /// <param name="logger">Logger estruturado (sem credenciais nos logs — RNF-6.3).</param>
    public SecretManagerProvider(
        SecretManagerServiceClient client,
        IMemoryCache cache,
        IOptions<SecretManagerOptions> options,
        ILogger<SecretManagerProvider> logger)
    {
        _client = client;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GetSecretAsync(
        string secretName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
            throw new ArgumentException("O nome do segredo não pode ser vazio.", nameof(secretName));

        // Cache hit — evita chamada ao Secret Manager dentro do TTL (DD-007)
        if (_cache.TryGetValue(BuildCacheKey(secretName), out string? cached) && cached is not null)
        {
            _logger.LogDebug(
                "Segredo '{SecretName}' recuperado do cache. (DD-007)",
                secretName);
            return cached;
        }

        // Cache miss — busca no GCP Secret Manager
        _logger.LogDebug(
            "Cache miss para segredo '{SecretName}'. Buscando no Secret Manager. (DD-007)",
            secretName);

        try
        {
            var resourceName = BuildSecretVersionName(secretName);
            var response = await _client.AccessSecretVersionAsync(resourceName, cancellationToken)
                .ConfigureAwait(false);

            var secretValue = response.Payload.Data.ToStringUtf8();

            // Armazena no cache com TTL configurável
            _cache.Set(BuildCacheKey(secretName), secretValue, _options.CacheTtl);

            _logger.LogInformation(
                "Segredo '{SecretName}' recuperado do Secret Manager e armazenado no cache por {TtlMinutes} min. (DD-007)",
                secretName,
                _options.CacheTtl.TotalMinutes);

            // NUNCA loga secretValue (RNF-6.3)
            return secretValue;
        }
        catch (Grpc.Core.RpcException ex)
        {
            // Log sem o valor da credencial (RNF-6.3, Req 10.4)
            _logger.LogError(
                "Falha ao acessar o Secret Manager para '{SecretName}'. " +
                "Status gRPC: {GrpcStatus}. (NOTIF-ERR-040, RISK-NOTIF-02)",
                secretName,
                ex.StatusCode);

            throw new SecretProviderException(
                secretName,
                $"Falha ao recuperar segredo '{secretName}' do Secret Manager. " +
                $"Status: {ex.StatusCode}.",
                ex);
        }
        catch (Exception ex) when (ex is not SecretProviderException)
        {
            _logger.LogError(
                "Exceção inesperada ao recuperar segredo '{SecretName}'. " +
                "Tipo: {ExceptionType}. (NOTIF-ERR-040)",
                secretName,
                ex.GetType().Name);

            throw new SecretProviderException(
                secretName,
                $"Exceção inesperada ao recuperar segredo '{secretName}'.",
                ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cache.Dispose();
            _disposed = true;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------------------------

    /// <summary>
    /// Monta a chave de cache para o segredo (prefixada para evitar colisão).
    /// </summary>
    private static string BuildCacheKey(string secretName) =>
        $"nd:secret:{secretName}";

    /// <summary>
    /// Monta o nome de recurso GCP Secret Manager no formato esperado pela API.
    /// Formato: <c>projects/{projectId}/secrets/{secretName}/versions/latest</c>.
    /// </summary>
    private SecretVersionName BuildSecretVersionName(string secretName) =>
        new(_options.ProjectId, secretName, "latest");
}
