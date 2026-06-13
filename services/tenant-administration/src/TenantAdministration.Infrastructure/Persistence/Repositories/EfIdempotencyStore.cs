using Microsoft.EntityFrameworkCore;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IIdempotencyStore"/> baseada em tabela PostgreSQL.
/// Usa a tabela <c>tenant_provisioning_requests</c> para armazenar e recuperar
/// resultados de comandos idempotentes (design.md §6.5).
/// </summary>
public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private readonly TenantAdministrationDbContext _db;

    /// <param name="db">DbContext do módulo.</param>
    public EfIdempotencyStore(TenantAdministrationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var request = await _db.ProvisioningRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdempotencyKey == key && r.Status == ProvisioningRequestStatus.Succeeded, ct);

        if (request is null)
            return null;

        // Serializa o resultado disponível como JSON mínimo para retorno
        if (request.ResultTenantId.HasValue && request.ResultIdentityTenantId is not null)
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                tenantId = request.ResultTenantId,
                identityTenantId = request.ResultIdentityTenantId
            });
        }

        return null;
    }

    /// <inheritdoc/>
    public ValueTask SetAsync(string key, string resultJson, CancellationToken ct = default)
    {
        // O armazenamento do resultado da idempotência é feito pela própria saga
        // ao atualizar o status de TenantProvisioningRequest para Succeeded.
        // Este método é um no-op aqui pois a saga já persiste diretamente.
        return ValueTask.CompletedTask;
    }
}
