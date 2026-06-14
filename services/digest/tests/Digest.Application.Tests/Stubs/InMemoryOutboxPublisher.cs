using Digest.Application.Abstractions;
using Digest.Domain.Events;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IOutboxPublisher"/> para testes da Onda 3.
/// Acumula eventos publicados para verificação em testes.
/// </summary>
public sealed class InMemoryOutboxPublisher : IOutboxPublisher
{
    private readonly List<DigestEmailSent> _published = [];

    /// <summary>Eventos publicados durante o teste.</summary>
    public IReadOnlyList<DigestEmailSent> Published => _published.AsReadOnly();

    /// <inheritdoc/>
    public Task PublishAsync(DigestEmailSent domainEvent, CancellationToken cancellationToken = default)
    {
        _published.Add(domainEvent);
        return Task.CompletedTask;
    }
}
