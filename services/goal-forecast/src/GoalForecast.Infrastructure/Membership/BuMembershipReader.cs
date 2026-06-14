using GoalForecast.Application.Ports;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GoalForecast.Infrastructure.Membership;

/// <summary>
/// Implementação concreta da porta <see cref="IBuMembershipReader"/>.
/// Consulta a tabela <c>bu_members</c> do contexto organization in-process
/// para verificar se um owner é membro ativo de uma BU (Req 1.4, DD-004).
///
/// Acesso direto por Npgsql (sem EF Core) — a tabela pertence ao BC organization,
/// fora do DbContext do goal-forecast. A string de conexão é a mesma do banco
/// compartilhado (monolito modular) ou do serviço organization (microsserviço).
///
/// Mapeia: Req 1.4, DD-004, design §6.5, TASK-20.
/// </summary>
public sealed class BuMembershipReader : IBuMembershipReader
{
    private readonly string _connectionString;
    private readonly ILogger<BuMembershipReader> _logger;

    /// <summary>
    /// Cria o reader com a string de conexão do banco de organization.
    /// </summary>
    public BuMembershipReader(string connectionString, ILogger<BuMembershipReader> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsOwnerMemberOfBu(
        Guid tenantId,
        Guid ownerId,
        Guid buId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(1)
                FROM bu_members
                WHERE tenant_id = @tenantId
                  AND owner_id  = @ownerId
                  AND bu_id     = @buId
                  AND active    = true
                """;

            cmd.Parameters.AddWithValue("tenantId", tenantId);
            cmd.Parameters.AddWithValue("ownerId", ownerId);
            cmd.Parameters.AddWithValue("buId", buId);

            var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "BuMembershipReader: falha ao verificar membership. " +
                "tenant_id={TenantId} owner_id={OwnerId} bu_id={BuId}. " +
                "Retornando false (defensivo).",
                tenantId, ownerId, buId);
            return false;
        }
    }
}
