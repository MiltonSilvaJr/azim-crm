using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Repositories;

/// <summary>
/// Porta de geração atômica de OpportunityNumber por tenant.
/// Implementação usa contador transacional com lock por tenant (DD-001, PBT-01).
/// Mapeia: Req 3, INV-5, DD-001, design §6.1.
/// </summary>
public interface IOpportunityNumberGenerator
{
    /// <summary>
    /// Gera o próximo número de oportunidade único para o tenant.
    /// Atômico e transacional — nunca duplica (DD-001).
    /// </summary>
    Task<OpportunityNumber> NextAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
