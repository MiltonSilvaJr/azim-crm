namespace ActivityManagement.Infrastructure.ReadPorts;

using ActivityManagement.Application.Ports;

/// <summary>
/// Implementação de <see cref="IAccountReadPort"/> via HTTP interno (mTLS, design §6.4).
/// Chama o serviço de account-management para validar existência de contas.
///
/// Padrão fail-closed (Req 3.4):
/// - Qualquer falha HTTP retorna <c>false</c>.
/// - Nunca propaga exceção para a camada de Application.
///
/// Em produção o <see cref="HttpClient"/> é configurado com Polly via DI.
/// Mapeia: TASK-17, design §6.4, Req 3.
/// </summary>
internal sealed class AccountReadAdapter : IAccountReadPort
{
    private readonly HttpClient _httpClient;

    /// <summary>Inicializa o adapter com o HttpClient configurado (injetado via DI).</summary>
    public AccountReadAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(
        Guid              accountId,
        Guid              tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"internal/v1/accounts/{accountId}?tenantId={tenantId}",
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Fail-closed: qualquer falha de rede/timeout → false
            return false;
        }
    }
}
