using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Ports;

/// <summary>
/// Porta de leitura do serviço organization (upstream conformist).
/// Valida usuários, BUs, estágios, canais e motivos de perda.
/// Mapeia: Req 2, Req 4, Req 5, Req 9, Req 10, design §6.4.
/// </summary>
public interface IOrganizationReadPort
{
    /// <summary>Valida que owner_id é usuário ativo com membership na bu_id.</summary>
    Task<bool> ValidateOwnerMembershipAsync(Guid tenantId, Guid buId, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Lê StageRef por ID com probabilidade default.</summary>
    Task<StageRef?> GetStageRefAsync(Guid tenantId, Guid stageId, CancellationToken cancellationToken = default);

    /// <summary>Lê OriginChannelRef por ID.</summary>
    Task<OriginChannelRef?> GetOriginChannelRefAsync(Guid tenantId, Guid originChannelId, CancellationToken cancellationToken = default);

    /// <summary>Lê LossReasonRef por ID.</summary>
    Task<LossReasonRef?> GetLossReasonRefAsync(Guid tenantId, Guid lossReasonId, CancellationToken cancellationToken = default);

    /// <summary>Retorna a ordem do estágio "Proposta Enviada" configurado para a BU.</summary>
    Task<int> GetPropostaEnviadaOrderAsync(Guid tenantId, Guid buId, CancellationToken cancellationToken = default);
}
