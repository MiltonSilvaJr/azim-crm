using Digest.Application.Abstractions;

namespace Digest.Api.Infrastructure;

/// <summary>
/// Implementação no-op de <see cref="IEmailSender"/> para o MVP do worker.
/// Em produção, substitui por adaptador do módulo notification-delivery (ADR-0005).
/// Health check: retorna sempre "saudável" (nenhuma dependência real).
/// </summary>
public sealed class NoOpEmailSender : IEmailSender
{
    /// <inheritdoc/>
    public Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default)
    {
        // MVP: retorna sucesso simulado com message_id gerado localmente.
        // Substituir por chamada ao notification-delivery quando o módulo estiver disponível.
        var messageId = $"noop-{Guid.NewGuid():N}";
        return Task.FromResult(new SendResult(messageId, Success: true));
    }
}
