namespace OpportunityPipeline.Domain.Opportunities.Ports;

/// <summary>
/// Porta de leitura do serviço account-management (upstream conformist).
/// Valida contas e contatos (sem PII — apenas IDs).
/// Mapeia: Req 16, design §6.4.
/// </summary>
public interface IAccountReadPort
{
    /// <summary>Verifica se account_id existe no tenant.</summary>
    Task<bool> AccountExistsAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Verifica se contact_id pertence à conta.</summary>
    Task<bool> ContactBelongsToAccountAsync(Guid tenantId, Guid accountId, Guid contactId, CancellationToken cancellationToken = default);
}
