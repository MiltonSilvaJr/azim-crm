namespace ActivityManagement.Infrastructure.Persistence;

using ActivityManagement.Application.Behaviors;
using ActivityManagement.Domain.Activities.Events;
using ActivityManagement.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implementação de <see cref="IUnitOfWork"/> para o módulo activity-management.
/// Responsável por gravar eventos de domínio no Outbox e fazer commit atômico
/// de escrita de negócio + Outbox + auditoria na mesma transação EF Core.
/// Chamado pelo <c>TransactionBehavior</c> após o handler (design §5.4, §6.6).
/// Mapeia: TASK-18, design §6.5, DD-007.
/// </summary>
internal sealed class ActivityManagementUnitOfWork : IUnitOfWork
{
    private readonly ActivityManagementDbContext _db;
    private readonly OutboxPublisher             _outboxPublisher;

    /// <summary>Inicializa a unit of work com o contexto EF e o publisher do Outbox.</summary>
    public ActivityManagementUnitOfWork(
        ActivityManagementDbContext db,
        OutboxPublisher             outboxPublisher)
    {
        _db              = db;
        _outboxPublisher = outboxPublisher;
    }

    /// <inheritdoc/>
    public async Task CommitAsync(
        IReadOnlyList<DomainEvent> domainEvents,
        CancellationToken          cancellationToken = default)
    {
        // Enfileira cada domain event no Outbox (na mesma transação EF)
        foreach (var domainEvent in domainEvents)
        {
            // Dedup key para ActivityOverdue: (activityId, scanDate)
            string? dedupKey = domainEvent is ActivityOverdue overdue
                ? $"{overdue.ActivityId}:{overdue.ScanDate:yyyy-MM-dd}"
                : null;

            await _outboxPublisher.EnqueueAsync(domainEvent, dedupKey, cancellationToken);
        }

        // SaveChanges faz commit atômico (escrita de negócio + Outbox + auditoria)
        await _db.SaveChangesAsync(cancellationToken);
    }
}
