using Digest.Application.Abstractions;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IEmailSender"/> para testes da Onda 3.
/// Conta chamadas de envio para verificação de idempotência (PBT-02).
/// </summary>
public sealed class InMemoryEmailSender : IEmailSender
{
    private int _sendCallCount;
    private bool _shouldFail;

    /// <summary>Número de chamadas a <see cref="SendAsync"/>.</summary>
    public int SendCallCount => _sendCallCount;

    /// <summary>Configura o stub para simular falha do provedor.</summary>
    public void SetFail(bool fail) => _shouldFail = fail;

    /// <inheritdoc/>
    public Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _sendCallCount);

        if (_shouldFail)
            return Task.FromResult(new SendResult("", Success: false));

        var messageId = $"msg-{Guid.NewGuid():N}";
        return Task.FromResult(new SendResult(messageId, Success: true));
    }
}
