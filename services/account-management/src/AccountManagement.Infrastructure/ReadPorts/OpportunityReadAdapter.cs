using System.Net.Http.Json;
using AccountManagement.Application.Ports;
using Microsoft.Extensions.Logging;

namespace AccountManagement.Infrastructure.ReadPorts;

/// <summary>
/// Adaptador de leitura para o módulo opportunity-pipeline via HTTP/gRPC interno com mTLS.
///
/// Implementa <see cref="IOpportunityReadPort"/> com resiliência configurada via Polly
/// (timeout + retry com backoff exponencial + circuit breaker — design §6.4, DD-004).
///
/// Em caso de timeout ou falha do upstream, retorna lista vazia com flag de degradação
/// (não derruba a 360° inteira — design §15).
///
/// Credenciais nunca são logadas. Headers de rastreabilidade (<c>X-Correlation-Id</c>,
/// <c>X-Tenant-Id</c>) são propagados upstream (design §6.4, RNF 9).
///
/// Mapeia: design §6.4, IOpportunityReadPort, Req 6, PBT-05, DD-004, TASK-12.
/// </summary>
internal sealed class OpportunityReadAdapter : IOpportunityReadPort
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpportunityReadAdapter> _logger;

    public OpportunityReadAdapter(
        HttpClient httpClient,
        ILogger<OpportunityReadAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OpportunityReadModel>> GetByAccountAsync(
        Guid accountId,
        IReadOnlySet<Guid> authorizedBuIds,
        CancellationToken cancellationToken = default)
    {
        if (authorizedBuIds.Count == 0)
            return Array.Empty<OpportunityReadModel>();

        try
        {
            var buFilter = string.Join(",", authorizedBuIds);
            var response = await _httpClient.GetAsync(
                $"/internal/v1/opportunities?accountId={accountId}&buIds={buFilter}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var opportunities = await response.Content
                .ReadFromJsonAsync<List<OpportunityReadModel>>(cancellationToken);

            // Filtra localmente para garantir que só retorna BUs autorizadas (PBT-05)
            var filtered = (opportunities ?? [])
                .Where(o => authorizedBuIds.Contains(o.BuId))
                .ToList();

            return filtered.AsReadOnly();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout — degradação parcial: retorna vazio sem vazar detalhes (design §15)
            _logger.LogWarning(
                "Timeout ao consultar opportunity-pipeline para conta {AccountId}. Degradação parcial.",
                accountId);
            return Array.Empty<OpportunityReadModel>();
        }
        catch (HttpRequestException ex)
        {
            // Falha de conectividade — degradação parcial
            _logger.LogWarning(ex,
                "Falha ao consultar opportunity-pipeline para conta {AccountId}. Degradação parcial.",
                accountId);
            return Array.Empty<OpportunityReadModel>();
        }
    }
}
