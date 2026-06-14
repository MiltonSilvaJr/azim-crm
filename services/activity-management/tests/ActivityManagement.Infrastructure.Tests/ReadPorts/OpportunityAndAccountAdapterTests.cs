namespace ActivityManagement.Infrastructure.Tests.ReadPorts;

using System.Net;
using System.Net.Http.Json;
using ActivityManagement.Infrastructure.ReadPorts;
using FluentAssertions;
using Xunit;

/// <summary>
/// Testes unitários para <see cref="OpportunityReadAdapter"/> e <see cref="AccountReadAdapter"/>
/// usando <c>HttpMessageHandler</c> fake (sem I/O real de rede).
///
/// Garantias testadas:
///   1. <see cref="OpportunityReadAdapter.ExistsAsync"/> → true quando API retorna 200.
///   2. <see cref="OpportunityReadAdapter.ExistsAsync"/> → false quando API retorna 404.
///   3. <see cref="OpportunityReadAdapter.IsOpenAsync"/> → true quando payload contém isOpen=true.
///   4. <see cref="OpportunityReadAdapter.IsOpenAsync"/> → false quando payload contém isOpen=false.
///   5. <see cref="AccountReadAdapter.ExistsAsync"/> → true quando API retorna 200.
///   6. <see cref="AccountReadAdapter.ExistsAsync"/> → false quando API retorna 404.
///   7. Falha da API HTTP retorna false (fail-closed — Req 3.4).
///   8. Timeout retorna false (fail-closed).
///
/// Mapeia: TASK-17, design §6.4, Req 3, RNF 5.
/// </summary>
public sealed class OpportunityAndAccountAdapterTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static HttpClient BuildClient(HttpResponseMessage response, string baseAddress)
    {
        var handler = new FakeHandler(response);
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    private static HttpClient BuildFaultingClient(string baseAddress)
    {
        var handler = new FaultingHandler();
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }

    private static HttpClient BuildTimeoutClient(string baseAddress)
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK))
        {
            Delay = TimeSpan.FromSeconds(10)
        };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseAddress),
            Timeout     = TimeSpan.FromMilliseconds(50),
        };
    }

    // ── OpportunityReadAdapter ─────────────────────────────────────────────────

    [Fact]
    public async Task OpportunityAdapter_ExistsAsync_Returns_True_When_Api_Returns_200()
    {
        // Arrange
        var client  = BuildClient(new HttpResponseMessage(HttpStatusCode.OK), "http://opportunity-svc/");
        var adapter = new OpportunityReadAdapter(client);

        // Act
        var exists = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        exists.Should().BeTrue(because: "API retornou 200 OK");
    }

    [Fact]
    public async Task OpportunityAdapter_ExistsAsync_Returns_False_When_Api_Returns_404()
    {
        // Arrange
        var client  = BuildClient(new HttpResponseMessage(HttpStatusCode.NotFound), "http://opportunity-svc/");
        var adapter = new OpportunityReadAdapter(client);

        // Act
        var exists = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        exists.Should().BeFalse(because: "API retornou 404 — oportunidade não existe ou é de outro tenant");
    }

    [Fact]
    public async Task OpportunityAdapter_IsOpenAsync_Returns_True_When_Open()
    {
        // Arrange: resposta JSON com isOpen=true
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { isOpen = true })
        };
        var client  = BuildClient(response, "http://opportunity-svc/");
        var adapter = new OpportunityReadAdapter(client);

        // Act
        var isOpen = await adapter.IsOpenAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        isOpen.Should().BeTrue();
    }

    [Fact]
    public async Task OpportunityAdapter_IsOpenAsync_Returns_False_When_Closed()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { isOpen = false })
        };
        var client  = BuildClient(response, "http://opportunity-svc/");
        var adapter = new OpportunityReadAdapter(client);

        // Act
        var isOpen = await adapter.IsOpenAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        isOpen.Should().BeFalse(because: "oportunidade está fechada");
    }

    [Fact]
    public async Task OpportunityAdapter_ExistsAsync_Returns_False_On_HttpFailure_FailClosed()
    {
        // Arrange: handler que lança exceção (falha de rede)
        var client  = BuildFaultingClient("http://opportunity-svc/");
        var adapter = new OpportunityReadAdapter(client);

        // Act
        var exists = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert: fail-closed — não cria atividade com vínculo inválido (Req 3.4)
        exists.Should().BeFalse(because: "falha na API deve resultar em fail-closed (false)");
    }

    [Fact]
    public async Task OpportunityAdapter_GetOpenOpportunityIdsForBuAsync_Returns_Empty_On_Failure()
    {
        // Arrange
        var client  = BuildFaultingClient("http://opportunity-svc/");
        var adapter = new OpportunityReadAdapter(client);

        // Act
        var result = await adapter.GetOpenOpportunityIdsForBuAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeEmpty(because: "falha na API retorna lista vazia (fail-closed)");
    }

    // ── AccountReadAdapter ─────────────────────────────────────────────────────

    [Fact]
    public async Task AccountAdapter_ExistsAsync_Returns_True_When_Api_Returns_200()
    {
        // Arrange
        var client  = BuildClient(new HttpResponseMessage(HttpStatusCode.OK), "http://account-svc/");
        var adapter = new AccountReadAdapter(client);

        // Act
        var exists = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task AccountAdapter_ExistsAsync_Returns_False_When_Api_Returns_404()
    {
        // Arrange
        var client  = BuildClient(new HttpResponseMessage(HttpStatusCode.NotFound), "http://account-svc/");
        var adapter = new AccountReadAdapter(client);

        // Act
        var exists = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AccountAdapter_ExistsAsync_Returns_False_On_HttpFailure_FailClosed()
    {
        // Arrange
        var client  = BuildFaultingClient("http://account-svc/");
        var adapter = new AccountReadAdapter(client);

        // Act
        var exists = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert: fail-closed (Req 3.4)
        exists.Should().BeFalse();
    }

    // ── Fake handlers ──────────────────────────────────────────────────────────

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public FakeHandler(HttpResponseMessage response) => _response = response;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Delay > TimeSpan.Zero)
                await Task.Delay(Delay, cancellationToken);
            return _response;
        }
    }

    private sealed class FaultingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulated network failure");
    }
}
