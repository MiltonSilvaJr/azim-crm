using System.Net;
using System.Net.Http.Json;
using AccountManagement.Application.Ports;
using AccountManagement.Infrastructure.ReadPorts;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountManagement.Infrastructure.Tests.ReadPorts;

/// <summary>
/// Testes unitários para <see cref="ActivityReadAdapter"/> com stub de HttpMessageHandler.
///
/// Cobre TASK-12 (ST-01, ST-02):
/// - Retorna atividades da conta quando upstream responde com sucesso.
/// - Degradação parcial: retorna lista vazia em timeout (design §15).
/// - Degradação parcial: retorna lista vazia em HttpRequestException.
/// - Degradação parcial: retorna lista vazia em resposta 5xx.
/// - Trata corpo nulo do upstream sem exceção.
///
/// Nenhuma rede real é usada — HttpMessageHandler é substituído por um stub.
///
/// Mapeia: TASK-12 (ST-01, ST-02), design §6.4, design §15, DD-004.
/// </summary>
public sealed class ActivityReadAdapterTests
{
    private static readonly Uri BaseUri = new("http://activity-management");

    // =========================================================================
    // ST-01: sucesso — retorna atividades do upstream
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_activities_on_success()
    {
        var accountId = Guid.NewGuid();
        var serverResponse = new List<ActivityReadModel>
        {
            new(Guid.NewGuid(), "reunião", "Reunião de kick-off", DateTimeOffset.UtcNow.AddDays(-3)),
            new(Guid.NewGuid(), "ligação", "Follow-up comercial", DateTimeOffset.UtcNow.AddDays(-1))
        };

        var adapter = BuildAdapter(HttpStatusCode.OK, serverResponse);

        var result = await adapter.GetByAccountAsync(accountId);

        result.Should().HaveCount(2, "upstream retornou 2 atividades");
        result[0].Type.Should().Be("reunião");
        result[1].Type.Should().Be("ligação");
    }

    // =========================================================================
    // ST-02: degradação parcial — timeout retorna lista vazia
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_on_timeout()
    {
        var handler = new StubHttpMessageHandler(
            _ => throw new TaskCanceledException("Timeout simulado",
                new TimeoutException("upstream timeout")));

        var httpClient = new HttpClient(handler) { BaseAddress = BaseUri };
        var adapter = new ActivityReadAdapter(httpClient, NullLogger<ActivityReadAdapter>.Instance);

        var result = await adapter.GetByAccountAsync(Guid.NewGuid());

        result.Should().BeEmpty(
            "timeout deve resultar em degradação parcial — lista vazia (design §15)");
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
        var adapter = new ActivityReadAdapter(httpClient, NullLogger<ActivityReadAdapter>.Instance);

        var result = await adapter.GetByAccountAsync(Guid.NewGuid());

        result.Should().BeEmpty(
            "falha de conectividade deve resultar em degradação parcial (design §15)");
    }

    // =========================================================================
    // ST-02: degradação parcial — upstream retorna 5xx
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_on_server_error_response()
    {
        var adapter = BuildAdapter(HttpStatusCode.InternalServerError, null);

        var result = await adapter.GetByAccountAsync(Guid.NewGuid());

        result.Should().BeEmpty(
            "resposta 5xx do upstream deve resultar em degradação parcial (design §15)");
    }

    // =========================================================================
    // ST-01: upstream retorna lista nula — sem exceção
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_when_upstream_returns_null_body()
    {
        var adapter = BuildAdapter(HttpStatusCode.OK, null);

        var result = await adapter.GetByAccountAsync(Guid.NewGuid());

        result.Should().BeEmpty("null do upstream deve ser tratado como lista vazia");
    }

    // =========================================================================
    // ST-01: upstream retorna lista vazia
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_returns_empty_when_upstream_returns_empty_list()
    {
        var adapter = BuildAdapter(HttpStatusCode.OK, new List<ActivityReadModel>());

        var result = await adapter.GetByAccountAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    // =========================================================================
    // ST-01: CancellationToken do caller é propagado e causa OperationCanceledException
    // =========================================================================

    [Fact]
    public async Task GetByAccountAsync_propagates_caller_cancellation()
    {
        // Token do caller já cancelado antes da chamada
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = new StubHttpMessageHandler(async (req) =>
        {
            await Task.Delay(5000, req.Options
                .TryGetValue(new HttpRequestOptionsKey<CancellationToken>("ct"), out var token)
                    ? token : CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = BaseUri };
        var adapter = new ActivityReadAdapter(httpClient, NullLogger<ActivityReadAdapter>.Instance);

        // Quando o token do caller está cancelado, OperationCanceledException deve propagar
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await adapter.GetByAccountAsync(Guid.NewGuid(), cts.Token));
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static ActivityReadAdapter BuildAdapter(
        HttpStatusCode statusCode,
        List<ActivityReadModel>? responseBody)
    {
        var handler = new StubHttpMessageHandler(
            _ =>
            {
                var response = new HttpResponseMessage(statusCode);
                if (responseBody is not null)
                    response.Content = JsonContent.Create(responseBody);
                return Task.FromResult(response);
            });

        var httpClient = new HttpClient(handler) { BaseAddress = BaseUri };
        return new ActivityReadAdapter(httpClient, NullLogger<ActivityReadAdapter>.Instance);
    }
}
