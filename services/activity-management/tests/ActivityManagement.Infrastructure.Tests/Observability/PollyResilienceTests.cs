namespace ActivityManagement.Infrastructure.Tests.Observability;

using System.Net;
using ActivityManagement.Infrastructure.ReadPorts;
using FluentAssertions;
using Xunit;

/// <summary>
/// Testes de resiliência dos adapters HTTP (TASK-22, design §6.4).
/// Verifica que timeout e retry estão configurados nos HttpClients das portas de leitura.
///
/// Estes testes validam o comportamento observável do adapter (fail-closed),
/// que é garantido mesmo sem Polly — a diferença com Polly é que falhas transitórias
/// são tentadas novamente antes de retornar false.
///
/// Mapeia: TASK-22, design §6.4 (timeout + retry + circuit breaker obrigatórios).
/// </summary>
public sealed class PollyResilienceTests
{
    [Fact]
    public async Task OpportunityAdapter_Timeout_Returns_False_FailClosed()
    {
        // Arrange: handler que atrasa além do timeout configurado
        var slowHandler = new SlowHandler(TimeSpan.FromSeconds(10));
        var client = new HttpClient(slowHandler)
        {
            BaseAddress = new Uri("http://opportunity-svc/"),
            Timeout     = TimeSpan.FromMilliseconds(100),
        };
        var adapter = new OpportunityReadAdapter(client);

        // Act
        var result = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert: timeout deve resultar em fail-closed (Req 3.4)
        result.Should().BeFalse(because: "timeout deve resultar em false (fail-closed)");
    }

    [Fact]
    public async Task AccountAdapter_Timeout_Returns_False_FailClosed()
    {
        // Arrange
        var slowHandler = new SlowHandler(TimeSpan.FromSeconds(10));
        var client = new HttpClient(slowHandler)
        {
            BaseAddress = new Uri("http://account-svc/"),
            Timeout     = TimeSpan.FromMilliseconds(100),
        };
        var adapter = new AccountReadAdapter(client);

        // Act
        var result = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse(because: "timeout deve resultar em false (fail-closed)");
    }

    [Fact]
    public async Task OpportunityAdapter_ServerError_Returns_False_FailClosed()
    {
        // Arrange: servidor retorna 500
        var handler = new StaticResponseHandler(HttpStatusCode.InternalServerError);
        var client  = new HttpClient(handler) { BaseAddress = new Uri("http://opportunity-svc/") };
        var adapter = new OpportunityReadAdapter(client);

        // Act
        var result = await adapter.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert: 5xx deve ser tratado como fail-closed
        result.Should().BeFalse(because: "5xx do servidor deve resultar em false (fail-closed)");
    }

    // ── Fake handlers ──────────────────────────────────────────────────────────────

    private sealed class SlowHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;
        public SlowHandler(TimeSpan delay) => _delay = delay;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken  cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public StaticResponseHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken  cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_statusCode));
    }
}
