namespace NotificationDelivery.Infrastructure.Tests.Senders;

/// <summary>
/// Stub de <see cref="HttpMessageHandler"/> para testes de senders sem rede real.
///
/// Permite configurar respostas HTTP predefinidas por chamada, interceptando
/// as requisições antes de enviá-las ao provedor real (sem rede, sem credencial real).
///
/// Registra a última requisição capturada para asserções de headers e payload.
/// </summary>
public sealed class HttpMessageHandlerStub : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _capturedRequests = [];

    /// <summary>Fila de respostas a retornar em ordem (FIFO).</summary>
    public void EnqueueResponse(HttpResponseMessage response) =>
        _responses.Enqueue(response);

    /// <summary>
    /// Enfileira uma resposta simples com status e body JSON.
    /// </summary>
    public void EnqueueJsonResponse(System.Net.HttpStatusCode status, string jsonBody)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
        };
        _responses.Enqueue(response);
    }

    /// <summary>Requisições capturadas, em ordem de chegada.</summary>
    public IReadOnlyList<HttpRequestMessage> CapturedRequests => _capturedRequests.AsReadOnly();

    /// <summary>Última requisição capturada (para asserções simples).</summary>
    public HttpRequestMessage? LastRequest => _capturedRequests.LastOrDefault();

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _capturedRequests.Add(request);

        if (_responses.TryDequeue(out var response))
            return Task.FromResult(response);

        // Sem resposta configurada — retorna 500 como fallback seguro
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) { /* sem recursos a liberar */ }
}
