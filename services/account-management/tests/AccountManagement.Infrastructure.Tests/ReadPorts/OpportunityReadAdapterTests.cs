using System.Net;
using System.Net.Http.Json;
using AccountManagement.Application.Ports;
using AccountManagement.Infrastructure.ReadPorts;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountManagement.Infrastructure.Tests.ReadPorts;

/// <summary>
/// Testes unitários para <see cref="OpportunityReadAdapter"/> com stub de HttpMessageHandler.
///
/// Cobre TASK-12 (ST-01, ST-02):
/// - Retorna oportunidades filtradas por BUs autorizadas (PBT-05).
/// - Retorna lista vazia para authorizedBuIds vazio (sem chamada HTTP).
/// - Degradação parcial: retorna lista vazia em timeout (OperationCanceledException não do token).
/// - Degradação parcial: retorna lista vazia em HttpRequestException (falha de conectividade).
/// - Filtro local: não retorna oportunidades de BUs não autorizadas mesmo que o upstream as retorne.
///
/// Nenhuma rede real é usada — HttpMessageHandler é substituído por um stub.
///
/// Mapeia: TASK-12 (ST-01, ST-02), design §6.4, design §15, PBT-05, DD-004.
/// </summary>
public sealed class OpportunityReadAdapterTests
{
    private static readonly Uri BaseUri = new("http://opportunity-pipeline");

    // =========================================================================
    // ST-01: sucesso — retorna oportunidades filtradas por BUs autorizadas
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_opportunities_for_authorized_bus()
    {
        var accountId = Guid.NewGuid();
        var authorizedBuId = Guid.NewGuid();
        var unauthorizedBuId = Guid.NewGuid();

        var serverResponse = new List<OpportunityReadModel>
        {
            new(Guid.NewGuid(), authorizedBuId, "Oportunidade Autorizada", "proposal", 50_000L),
            new(Guid.NewGuid(), unauthorizedBuId, "Oportunidade NÃO Autorizada", "qualified", 30_000L)
        };

        var adapter = BuildAdapter(HttpStatusCode.OK, serverResponse);
        var authorizedBuIds = new HashSet<Guid> { authorizedBuId };

        var result = await adapter.GetByAccountAsync(accountId, authorizedBuIds);

        result.Should().HaveCount(1, "apenas oportunidades de BUs autorizadas são retornadas (PBT-05)");
        result[0].BuId.Should().Be(authorizedBuId);
        result.Should().NotContain(o => o.BuId == unauthorizedBuId,
            "filtro local garante que BUs não autorizadas são removidas mesmo que o upstream as retorne");
    }

    [Fact]
    public async Task GetByAccountAsync_returns_all_when_all_bus_authorized()
    {
        var accountId = Guid.NewGuid();
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();

        var serverResponse = new List<OpportunityReadModel>
        {
            new(Guid.NewGuid(), bu1, "Op 1", "open", 10_000L),
            new(Guid.NewGuid(), bu2, "Op 2", "proposal", 20_000L)
        };

        var adapter = BuildAdapter(HttpStatusCode.OK, serverResponse);
        var authorizedBuIds = new HashSet<Guid> { bu1, bu2 };

        var result = await adapter.GetByAccountAsync(accountId, authorizedBuIds);

        result.Should().HaveCount(2, "todas as oportunidades retornadas pelo upstream são autorizadas");
    }

    // =========================================================================
    // ST-01: retorno vazio quando authorizedBuIds está vazio — sem chamada HTTP
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_without_http_call_when_buIds_empty()
    {
        var callCount = 0;
        var handler = new StubHttpMessageHandler(
            _ =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

        var httpClient = new HttpClient(handler) { BaseAddress = BaseUri };
        var adapter = new OpportunityReadAdapter(httpClient, NullLogger<OpportunityReadAdapter>.Instance);

        var result = await adapter.GetByAccountAsync(Guid.NewGuid(), new HashSet<Guid>());

        result.Should().BeEmpty("sem BUs autorizadas não há nada a consultar");
        callCount.Should().Be(0, "sem BUs autorizadas nenhuma chamada HTTP deve ser feita");
    }

    // =========================================================================
    // ST-02: degradação parcial — timeout retorna lista vazia
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_on_timeout()
    {
        // Simula timeout: lança OperationCanceledException sem que o token do caller seja cancelado
        var handler = new StubHttpMessageHandler(
            _ => throw new TaskCanceledException("Timeout simulado",
                new TimeoutException("upstream timeout")));

        var httpClient = new HttpClient(handler) { BaseAddress = BaseUri };
        var adapter = new OpportunityReadAdapter(httpClient, NullLogger<OpportunityReadAdapter>.Instance);

        var result = await adapter.GetByAccountAsync(
            Guid.NewGuid(), new HashSet<Guid> { Guid.NewGuid() });

        result.Should().BeEmpty(
            "timeout deve ser tratado como degradação parcial — retorna lista vazia (design §15)");
    }

    // =========================================================================
    // ST-02: degradação parcial — HttpRequestException retorna lista vazia
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_on_http_request_exception()
    {
        var handler = new StubHttpMessageHandler(
            _ => throw new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handler) { BaseAddress = BaseUri };
        var adapter = new OpportunityReadAdapter(httpClient, NullLogger<OpportunityReadAdapter>.Instance);

        var result = await adapter.GetByAccountAsync(
            Guid.NewGuid(), new HashSet<Guid> { Guid.NewGuid() });

        result.Should().BeEmpty(
            "falha de conectividade deve resultar em degradação parcial — lista vazia (design §15)");
    }

    // =========================================================================
    // ST-02: degradação parcial — upstream retorna 5xx
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_on_server_error_response()
    {
        var adapter = BuildAdapter(HttpStatusCode.ServiceUnavailable, (List<OpportunityReadModel>?)null);

        var result = await adapter.GetByAccountAsync(
            Guid.NewGuid(), new HashSet<Guid> { Guid.NewGuid() });

        result.Should().BeEmpty(
            "resposta 5xx deve resultar em degradação parcial — lista vazia (design §15)");
    }

    // =========================================================================
    // ST-01: upstream retorna lista nula — retorna lista vazia sem exceção
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_when_upstream_returns_null_body()
    {
        var adapter = BuildAdapter(HttpStatusCode.OK, (List<OpportunityReadModel>?)null);
        var authorizedBuIds = new HashSet<Guid> { Guid.NewGuid() };

        var result = await adapter.GetByAccountAsync(Guid.NewGuid(), authorizedBuIds);

        result.Should().BeEmpty("null do upstream deve ser tratado como lista vazia");
    }

    // =========================================================================
    // ST-01: upstream retorna lista vazia
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_when_upstream_returns_empty_list()
    {
        var adapter = BuildAdapter(HttpStatusCode.OK, new List<OpportunityReadModel>());
        var authorizedBuIds = new HashSet<Guid> { Guid.NewGuid() };

        var result = await adapter.GetByAccountAsync(Guid.NewGuid(), authorizedBuIds);

        result.Should().BeEmpty();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static OpportunityReadAdapter BuildAdapter(
        HttpStatusCode statusCode,
        List<OpportunityReadModel>? responseBody)
    {
        var handler = new StubHttpMessageHandler(
            _ =>
            {
                if (responseBody is null && statusCode != HttpStatusCode.OK)
                    return Task.FromResult(new HttpResponseMessage(statusCode));

                var response = new HttpResponseMessage(statusCode);
                if (responseBody is not null)
                    response.Content = JsonContent.Create(responseBody);
                return Task.FromResult(response);
            });

        var httpClient = new HttpClient(handler) { BaseAddress = BaseUri };
        return new OpportunityReadAdapter(httpClient, NullLogger<OpportunityReadAdapter>.Instance);
    }
}
