using Digest.Application.Repositories;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IEmailDigestLogRepository"/> para uso em testes da Onda 3.
/// Simula a UNIQUE constraint via dicionário em memória (PBT-02).
/// </summary>
public sealed class InMemoryEmailDigestLogRepository : IEmailDigestLogRepository
{
    // Chave: (tenantId, userId, digestDate)
    private readonly Dictionary<(Guid, Guid, string), DigestStatus> _records = [];

    // Contador de chamadas a ReserveAsync — para verificação em PBT-02
    private int _reserveCallCount;

    /// <summary>Número de vezes que <see cref="ReserveAsync"/> foi chamado.</summary>
    public int ReserveCallCount => _reserveCallCount;

    /// <inheritdoc/>
    public Task<ReservationResult> ReserveAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        Guid? correlationId,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _reserveCallCount);

        var key = (tenantId, userId, digestDate.ToString());

        if (_records.TryGetValue(key, out var existingStatus))
        {
            return existingStatus switch
            {
                DigestStatus.Sent or DigestStatus.Delivered or DigestStatus.Opened =>
                    Task.FromResult(ReservationResult.AlreadySent),
                DigestStatus.Scheduled =>
                    Task.FromResult(ReservationResult.AlreadyScheduled),
                _ => Task.FromResult(ReservationResult.AlreadySent),
            };
        }

        // Simula INSERT ON CONFLICT DO NOTHING — ocupa a vaga
        _records[key] = DigestStatus.Scheduled;
        return Task.FromResult(ReservationResult.Reserved);
    }

    /// <inheritdoc/>
    public Task MarkSentAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        var key = (tenantId, userId, digestDate.ToString());
        _records[key] = DigestStatus.Sent;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task MarkFailedAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        CancellationToken cancellationToken = default)
    {
        var key = (tenantId, userId, digestDate.ToString());
        _records[key] = DigestStatus.Failed;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateStatusByMessageIdAsync(
        string messageId,
        DigestStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        // No-op no stub — implementado em InMemory para UpdateDeliveryStatusHandler
        return Task.CompletedTask;
    }

    /// <summary>Retorna o status atual de um registro, ou null se não existir.</summary>
    public DigestStatus? GetStatus(Guid tenantId, Guid userId, DigestDate digestDate)
    {
        var key = (tenantId, userId, digestDate.ToString());
        return _records.TryGetValue(key, out var status) ? status : null;
    }
}
