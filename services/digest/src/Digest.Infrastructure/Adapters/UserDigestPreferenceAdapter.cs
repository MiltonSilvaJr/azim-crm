using System.Net.Http.Json;
using Digest.Application.Models;
using Digest.Application.Ports;
using Microsoft.Extensions.Logging;
using Polly;

namespace Digest.Infrastructure.Adapters;

/// <summary>
/// Adaptador de <see cref="IUserDigestPreferencePort"/> com resiliência Polly (TASK-19).
/// No MVP monolítico: leitura direta via HTTP interno do módulo organization (DD-003).
/// Timeout 10s, retry exponencial x3, circuit breaker (design §6.4, RNF 5).
/// Retorna preferência com <c>OptOut = false</c> quando fonte indisponível (padrão inclusivo).
/// </summary>
public sealed class UserDigestPreferenceAdapter : IUserDigestPreferencePort
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline<DigestPreference?> _singlePipeline;
    private readonly ResiliencePipeline<IReadOnlyList<DigestPreference>> _batchPipeline;

    /// <summary>
    /// Constrói o adaptador com o HttpClient nomeado e os pipelines Polly.
    /// </summary>
    public UserDigestPreferenceAdapter(HttpClient http, ILogger<UserDigestPreferenceAdapter> logger)
    {
        _http = http;
        _singlePipeline = ResiliencePipelineFactory.Create<DigestPreference?>(
            nameof(UserDigestPreferenceAdapter), logger);
        _batchPipeline = ResiliencePipelineFactory.CreateForList<DigestPreference>(
            $"{nameof(UserDigestPreferenceAdapter)}.Batch", logger);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Quando a fonte está indisponível, retorna <see cref="DigestPreference"/> com <c>OptOut = false</c>
    /// (padrão inclusivo — o digest continua funcionando sem opt-out explícito do usuário).
    /// </remarks>
    public async Task<DigestPreference> GetPreferenceAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pref = await _singlePipeline.ExecuteAsync(async ct =>
            {
                return await _http.GetFromJsonAsync<DigestPreference>(
                    $"/internal/organization/tenants/{tenantId}/users/{userId}/digest-preference",
                    ct);
            }, cancellationToken);

            // Fallback para padrão inclusivo quando não há registro
            return pref ?? new DigestPreference(userId, OptOut: false);
        }
        catch
        {
            // Degradação graciosa: padrão inclusivo quando fonte indisponível (RNF 5.4)
            return new DigestPreference(userId, OptOut: false);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DigestPreference>> GetAllPreferencesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _batchPipeline.ExecuteAsync(async ct =>
            {
                var items = await _http.GetFromJsonAsync<List<DigestPreference>>(
                    $"/internal/organization/tenants/{tenantId}/digest-preferences",
                    ct);
                return (IReadOnlyList<DigestPreference>)(items ?? []);
            }, cancellationToken) ?? Array.Empty<DigestPreference>();
        }
        catch (Exception ex) when (ex is HttpRequestException or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            return Array.Empty<DigestPreference>();
        }
    }
}
