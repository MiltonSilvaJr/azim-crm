using Digest.Application.Models;
using Digest.Application.Ports;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IActivityReadPort"/> para uso em testes da Onda 3.
/// Retorna listas configuráveis sem dependência de infraestrutura.
/// </summary>
public sealed class InMemoryActivityReadPort : IActivityReadPort
{
    private readonly List<ActivityItem> _overdue = [];
    private readonly List<ActivityItem> _today = [];

    /// <summary>Configura as atividades vencidas que serão retornadas.</summary>
    public void SetOverdue(IEnumerable<ActivityItem> items) =>
        _overdue.AddRange(items);

    /// <summary>Configura as atividades do dia que serão retornadas.</summary>
    public void SetToday(IEnumerable<ActivityItem> items) =>
        _today.AddRange(items);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ActivityItem>> GetOverdueActivitiesAsync(
        Guid tenantId, Guid ownerId, DateOnly referenceDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ActivityItem>>(
            _overdue.Where(a => a.OwnerId == ownerId).ToList().AsReadOnly());

    /// <inheritdoc/>
    public Task<IReadOnlyList<ActivityItem>> GetTodayActivitiesAsync(
        Guid tenantId, Guid ownerId, DateOnly referenceDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ActivityItem>>(
            _today.Where(a => a.OwnerId == ownerId).ToList().AsReadOnly());
}
