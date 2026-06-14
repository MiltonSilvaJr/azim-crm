namespace AccountManagement.Infrastructure.Tests.ReadPorts;

/// <summary>
/// Stub de <see cref="HttpMessageHandler"/> para testes unitários de adaptadores HTTP.
///
/// Permite simular respostas HTTP, timeouts e exceções de rede sem dependência de rede real.
/// Usado por <see cref="OpportunityReadAdapterTests"/> e <see cref="ActivityReadAdapterTests"/>.
///
/// Mapeia: TASK-12, design §6.4.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    /// <summary>
    /// Inicializa com uma função que recebe a requisição e retorna a resposta simulada.
    /// </summary>
    public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Propaga cancelamento do caller antes de invocar o handler
        cancellationToken.ThrowIfCancellationRequested();
        return _handler(request);
    }
}
