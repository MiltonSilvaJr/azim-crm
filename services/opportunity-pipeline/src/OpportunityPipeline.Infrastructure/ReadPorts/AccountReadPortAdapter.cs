using Microsoft.Extensions.Logging;
using OpportunityPipeline.Domain.Opportunities.Ports;
using System.Net.Http.Json;

namespace OpportunityPipeline.Infrastructure.ReadPorts;

/// <summary>
/// Adapter HTTP para IAccountReadPort — upstream conformist (design §6.4).
/// Sem cache de PII (design §6.2). Resiliência via Polly (TASK-17).
/// Mapeia: design §6.4, TASK-17.
/// </summary>
public sealed class AccountReadPortAdapter(
    HttpClient httpClient,
    ILogger<AccountReadPortAdapter> logger)
    : IAccountReadPort
{
    /// <inheritdoc/>
    public async Task<bool> AccountExistsAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient
                .GetAsync(
                    $"internal/accounts/tenants/{tenantId}/accounts/{accountId}",
                    cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "AccountAdapter: falha ao verificar existência de account_id={AccountId}.",
                accountId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ContactBelongsToAccountAsync(
        Guid tenantId,
        Guid accountId,
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient
                .GetAsync(
                    $"internal/accounts/tenants/{tenantId}/accounts/{accountId}/contacts/{contactId}",
                    cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "AccountAdapter: falha ao verificar contact_id={ContactId} na account_id={AccountId}.",
                contactId, accountId);
            return false;
        }
    }
}
