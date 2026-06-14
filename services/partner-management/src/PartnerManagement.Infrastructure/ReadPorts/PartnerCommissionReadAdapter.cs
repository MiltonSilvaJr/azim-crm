using System.Net.Http.Json;
using System.Text.Json;
using PartnerManagement.Application.Ports;

namespace PartnerManagement.Infrastructure.ReadPorts;

/// <summary>
/// Adaptador do read model <c>opportunity_partner_commissions</c> do opportunity-pipeline.
/// Implementa <see cref="IPartnerCommissionReadPort"/> consultando a API interna do pipeline via HTTP.
/// Nunca implementa fórmula de comissão — apenas lê e mapeia o resultado (DD-003, P3).
/// Propaga <c>correlation_id</c> e <c>tenant_id</c> em toda chamada (design §6.4).
/// Mapeia: Req 9, Req 10, DD-003, design §6.4, TASK-19.
/// </summary>
public sealed class PartnerCommissionReadAdapter : IPartnerCommissionReadPort
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Nome do cabeçalho de correlação propagado ao pipeline.
    /// </summary>
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// Nome do cabeçalho de tenant propagado ao pipeline.
    /// </summary>
    private const string TenantIdHeader = "X-Tenant-Id";

    /// <summary>
    /// Inicializa o adaptador com o HttpClient configurado para o pipeline.
    /// O HttpClient deve ter a base address do opportunity-pipeline configurada no DI.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP (registrado via IHttpClientFactory).</param>
    public PartnerCommissionReadAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommissionLine>> GetCommissionLinesAsync(
        Guid tenantId,
        Guid partnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        // Propaga correlation_id e tenant_id ao pipeline (design §6.4)
        using HttpRequestMessage request = BuildRequest(tenantId, partnerId, from, to, correlationId);

        HttpResponseMessage response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // Adapter não implementa fórmula; apenas mapeia o resultado (DD-003)
        if (!response.IsSuccessStatusCode)
        {
            // Em degradação: retorna lista vazia; o handler aplica degradação parcial
            return [];
        }

        CommissionLineDto[]? dtos = await response.Content
            .ReadFromJsonAsync<CommissionLineDto[]>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (dtos is null or { Length: 0 })
        {
            return [];
        }

        // Mapeia DTO → CommissionLine (sem soma — soma fica no handler)
        CommissionLine[] lines = dtos.Select(dto => new CommissionLine(
            OpportunityId: dto.OpportunityId,
            CommissionCents: dto.CommissionCents,
            IsSnapshot: dto.IsSnapshot,
            OccurredAt: dto.OccurredAt
        )).ToArray();

        return lines;
    }

    private static HttpRequestMessage BuildRequest(
        Guid tenantId,
        Guid partnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? correlationId)
    {
        // Formato ISO 8601 para parâmetros de data
        string fromStr = Uri.EscapeDataString(from.ToString("O"));
        string toStr = Uri.EscapeDataString(to.ToString("O"));

        string path = $"internal/commission-lines?partnerId={partnerId}&from={fromStr}&to={toStr}";

        HttpRequestMessage request = new(HttpMethod.Get, path);

        // Propaga headers de rastreabilidade (design §6.4)
        request.Headers.TryAddWithoutValidation(TenantIdHeader, tenantId.ToString());

        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdHeader, correlationId);
        }

        return request;
    }

    // =========================================================================
    // DTO interno (mapeamento da resposta do pipeline)
    // =========================================================================

    /// <summary>
    /// DTO de desserialização da resposta do pipeline.
    /// Interno ao adapter — não vaza para Application nem Domain.
    /// </summary>
    private sealed class CommissionLineDto
    {
        public Guid OpportunityId { get; init; }
        public long CommissionCents { get; init; }
        public bool IsSnapshot { get; init; }
        public DateTimeOffset OccurredAt { get; init; }
    }
}
