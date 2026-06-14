using Microsoft.EntityFrameworkCore;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositório EF Core para o aggregate root Opportunity.
/// Carrega agregado completo com transições, comissões e contatos.
/// Global Query Filter por tenant_id já aplicado pelo DbContext (ADR-0001).
/// Mapeia: design §6.1, TASK-13.
/// </summary>
public sealed class OpportunityRepository(OpportunityDbContext context) : IOpportunityRepository
{
    /// <inheritdoc/>
    public async Task<Opportunity?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Global Query Filter já filtra por tenant_id — tenantId passado apenas para RLS
        return await context.Opportunities
            .Include(o => o.Transitions)
            .Include(o => o.Commissions)
            .Include(o => o.Contacts)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Opportunity?> GetByNumberAsync(
        OpportunityNumber number,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await context.Opportunities
            .Include(o => o.Transitions)
            .Include(o => o.Commissions)
            .Include(o => o.Contacts)
            .FirstOrDefaultAsync(o => o.Number == number, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddAsync(
        Opportunity opportunity,
        CancellationToken cancellationToken = default)
    {
        await context.Opportunities.AddAsync(opportunity, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task SaveAsync(
        Opportunity opportunity,
        CancellationToken cancellationToken = default)
    {
        // Tracking ativo — EF detecta mudanças automaticamente.
        // SaveChanges é chamado pelo UnitOfWork.CommitAsync.
        context.Opportunities.Update(opportunity);
        return Task.CompletedTask;
    }
}
