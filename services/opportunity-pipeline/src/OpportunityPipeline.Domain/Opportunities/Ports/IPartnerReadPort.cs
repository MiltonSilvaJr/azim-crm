using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Ports;

/// <summary>
/// Porta de leitura do serviço partner-management (upstream conformist).
/// Valida parceiros e fornece defaults de comissão.
/// Mapeia: Req 11, design §6.4.
/// </summary>
public interface IPartnerReadPort
{
    /// <summary>Verifica se partner_id existe no tenant.</summary>
    Task<bool> PartnerExistsAsync(Guid tenantId, Guid partnerId, CancellationToken cancellationToken = default);

    /// <summary>Retorna defaults de comissão configurados no parceiro.</summary>
    Task<CommissionDefaults?> GetCommissionDefaultsAsync(Guid tenantId, Guid partnerId, CancellationToken cancellationToken = default);
}
