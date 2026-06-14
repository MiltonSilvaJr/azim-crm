using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Repositories;

/// <summary>
/// Interface de repositório para o aggregate root Opportunity.
/// Intenção de domínio: GetById, GetByNumber, Add, Save.
/// Implementação na camada Infrastructure.
/// Mapeia: design §4.1, §6.1.
/// </summary>
public interface IOpportunityRepository
{
    /// <summary>Carrega o agregado completo por ID (transições, comissão ativa, contatos).</summary>
    Task<Opportunity?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Carrega por número único de oportunidade.</summary>
    Task<Opportunity?> GetByNumberAsync(OpportunityNumber number, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Persiste novo agregado.</summary>
    Task AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default);

    /// <summary>Persiste alterações em agregado existente.</summary>
    Task SaveAsync(Opportunity opportunity, CancellationToken cancellationToken = default);
}
