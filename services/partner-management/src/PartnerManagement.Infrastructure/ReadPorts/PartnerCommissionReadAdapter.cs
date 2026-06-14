using System.Net.Http.Json;
using PartnerManagement.Application.Ports;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace PartnerManagement.Infrastructure.ReadPorts;

/// <summary>
/// Adaptador do read model <c>opportunity_partner_commissions</c> do opportunity-pipeline.
/// Implementa <see cref="IPartnerCommissionReadPort"/> consultando a API interna do pipeline via HTTP.
/// Nunca implementa fórmula de comissão — apenas lê e mapeia o resultado (DD-003, P3).
/// Propaga <c>correlation_id</c> e <c>tenant_id</c> em toda chamada (design §6.4).
/// Aplica resiliência via Polly: timeout por tentativa → retry com backoff → circuit breaker (TASK-28).
/// Em qualquer falha (timeout, 5xx, circuit breaker aberto), retorna lista vazia para que
/// o handler aplique degradação parcial sem retornar 5xx ao cliente (design §6.4, RISK-PM-06).
/// Mapeia: Req 9, Req 10, DD-003, design §6.4, TASK-19, TASK-28.
/// </summary>
public sealed class PartnerCommissionReadAdapter : IPartnerCommissionReadPort
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    /// <summary>
    /// Nome do cabeçalho de correlação propagado ao pipeline.
    /// </summary>
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// Nome do cabeçalho de tenant propagado ao pipeline.
    /// </summary>
    private const string TenantIdHeader = "X-Tenant-Id";

    /// <summary>
    /// Inicializa o adaptador com o HttpClient e pipeline de resiliência padrão.
    /// Usado pelo DI em produção.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP (registrado via IHttpClientFactory).</param>
    public PartnerCommissionReadAdapter(HttpClient httpClient)
        : this(httpClient, CommissionReadResiliencePipeline.Build(new CommissionReadResilienceOptions()))
    {
    }

    /// <summary>
    /// Inicializa o adaptador com HttpClient e pipeline de resiliência customizado.
    /// Usado em testes para injetar políticas específicas (ex.: sem delay).
    /// </summary>
    /// <param name="httpClient">Cliente HTTP.</param>
    /// <param name="pipeline">Pipeline de resiliência Polly.</param>
    internal PartnerCommissionReadAdapter(
        HttpClient httpClient,
        ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        _httpClient = httpClient;
        _pipeline = pipeline;
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
        try
        {
            HttpResponseMessage response = await _pipeline.ExecuteAsync(
                async ct =>
                {
                    using HttpRequestMessage request = BuildRequest(tenantId, partnerId, from, to, correlationId);
                    return await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

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
        catch (BrokenCircuitException)
        {
            // Circuit breaker aberto: degradação parcial imediata, sem retentativa
            return [];
        }
        catch (TimeoutRejectedException)
        {
            // Timeout Polly: todas as tentativas esgotaram o tempo configurado
            return [];
        }
        catch (HttpRequestException)
        {
            // Falha de rede após retentativas esgotadas
            return [];
        }
        catch (TaskCanceledException)
        {
            // Cancelamento do CancellationToken do chamador ou timeout do HttpClient
            return [];
        }
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
