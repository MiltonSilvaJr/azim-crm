using Digest.Application.Models;
using Digest.Application.Ports;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IOpportunityReadPort"/> para uso em testes da Onda 3.
/// </summary>
public sealed class InMemoryOpportunityReadPort : IOpportunityReadPort
{
    private readonly List<OpportunityItem> _stale = [];
    private readonly List<OpportunityItem> _overdueClosings = [];
    private readonly List<OpportunityItem> _pipeline = [];

    /// <summary>Configura as oportunidades estagnadas.</summary>
    public void SetStale(IEnumerable<OpportunityItem> items) =>
        _stale.AddRange(items);

    /// <summary>Configura os fechamentos vencidos.</summary>
    public void SetOverdueClosings(IEnumerable<OpportunityItem> items) =>
        _overdueClosings.AddRange(items);

    /// <summary>Configura o pipeline ponderado.</summary>
    public void SetPipeline(IEnumerable<OpportunityItem> items) =>
        _pipeline.AddRange(items);

    /// <inheritdoc/>
    public Task<IReadOnlyList<OpportunityItem>> GetStaleOpportunitiesAsync(
        Guid tenantId, Guid ownerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OpportunityItem>>(
            _stale.Where(o => o.OwnerId == ownerId).ToList().AsReadOnly());

    /// <inheritdoc/>
    public Task<IReadOnlyList<OpportunityItem>> GetOverdueClosingsAsync(
        Guid tenantId, Guid ownerId, DateOnly referenceDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OpportunityItem>>(
            _overdueClosings.Where(o => o.OwnerId == ownerId).ToList().AsReadOnly());

    /// <inheritdoc/>
    public Task<IReadOnlyList<OpportunityItem>> GetWeightedPipelineAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OpportunityItem>>(_pipeline.AsReadOnly());
}
