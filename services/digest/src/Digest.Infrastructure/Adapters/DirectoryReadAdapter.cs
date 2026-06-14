using System.Net.Http.Json;
using Digest.Application.Models;
using Digest.Application.Ports;
using Microsoft.Extensions.Logging;
using Polly;

namespace Digest.Infrastructure.Adapters;

/// <summary>
/// Adaptador de <see cref="IUserDirectoryPort"/> com resiliência Polly (TASK-19).
/// Nome: DirectoryReadAdapter (evita prefixo "User*" proibido pela Architecture rule — Req 6.2).
/// No MVP monolítico: leitura direta via HTTP interno do módulo organization.
/// Timeout 10s, retry exponencial x3, circuit breaker (design §6.4, RNF 5).
/// </summary>
public sealed class DirectoryReadAdapter : IUserDirectoryPort
{
    private readonly HttpClient _http;
    private readonly ResiliencePipeline<IReadOnlyList<TenantInfo>> _tenantPipeline;
    private readonly ResiliencePipeline<IReadOnlyList<UserInfo>> _userPipeline;

    /// <summary>
    /// Constrói o adaptador com o HttpClient nomeado e os pipelines Polly.
    /// </summary>
    public DirectoryReadAdapter(HttpClient http, ILogger<DirectoryReadAdapter> logger)
    {
        _http = http;
        _tenantPipeline = ResiliencePipelineFactory.CreateForList<TenantInfo>(
            $"{nameof(DirectoryReadAdapter)}.Tenants", logger);
        _userPipeline = ResiliencePipelineFactory.CreateForList<UserInfo>(
            $"{nameof(DirectoryReadAdapter)}.Users", logger);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenantInfo>> GetActiveTenantInfosAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _tenantPipeline.ExecuteAsync(async ct =>
            {
                var items = await _http.GetFromJsonAsync<List<TenantInfo>>(
                    "/internal/organization/tenants/active", ct);
                return (IReadOnlyList<TenantInfo>)(items ?? []);
            }, cancellationToken) ?? Array.Empty<TenantInfo>();
        }
        catch (Exception ex) when (ex is HttpRequestException or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            return Array.Empty<TenantInfo>();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserInfo>> GetActiveUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userPipeline.ExecuteAsync(async ct =>
            {
                var items = await _http.GetFromJsonAsync<List<UserInfo>>(
                    $"/internal/organization/tenants/{tenantId}/users/active", ct);
                return (IReadOnlyList<UserInfo>)(items ?? []);
            }, cancellationToken) ?? Array.Empty<UserInfo>();
        }
        catch (Exception ex) when (ex is HttpRequestException or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            return Array.Empty<UserInfo>();
        }
    }
}
