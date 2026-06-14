using System.Net.Http.Json;
using AccountManagement.Application.Ports;
using Microsoft.Extensions.Logging;

namespace AccountManagement.Infrastructure.ReadPorts;

/// <summary>
/// Adaptador de leitura para o módulo activity-management via HTTP/gRPC interno com mTLS.
///
/// Implementa <see cref="IActivityReadPort"/> com resiliência configurada via Polly
/// (timeout + retry + circuit breaker — design §6.4, DD-004).
///
/// Em caso de falha, aplica degradação parcial retornando lista vazia
/// (não derruba a 360° inteira — design §15).
///
/// Mapeia: design §6.4, IActivityReadPort, Req 6, DD-004, TASK-12.
/// </summary>
internal sealed class ActivityReadAdapter : IActivityReadPort
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ActivityReadAdapter> _logger;

    public ActivityReadAdapter(
        HttpClient httpClient,
        ILogger<ActivityReadAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityReadModel>> GetByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/internal/v1/activities?accountId={accountId}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var activities = await response.Content
                .ReadFromJsonAsync<List<ActivityReadModel>>(cancellationToken);

            return (activities ?? []).AsReadOnly();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Timeout ao consultar activity-management para conta {AccountId}. Degradação parcial.",
                accountId);
            return Array.Empty<ActivityReadModel>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Falha ao consultar activity-management para conta {AccountId}. Degradação parcial.",
                accountId);
            return Array.Empty<ActivityReadModel>();
        }
    }
}
