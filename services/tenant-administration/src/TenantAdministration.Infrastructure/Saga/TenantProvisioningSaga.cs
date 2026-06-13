using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.ValueObjects;
using TenantAdministration.Infrastructure.Identity;
using TenantAdministration.Infrastructure.Outbox;
using TenantAdministration.Infrastructure.Persistence;

namespace TenantAdministration.Infrastructure.Saga;

/// <summary>
/// Implementação da saga de provisionamento atômico de tenant (design.md §6.4, Req 12, PBT-07).
///
/// Fluxo:
/// (1) Verificar idempotência em <c>tenant_provisioning_requests</c>;
/// (2) Criar tenant no IdP com <paramref name="idempotencyKey"/>;
/// (3) Se falhar → abortar com TA-ERR-009;
/// (4) Se sucesso → BEGIN; INSERT tenant; INSERT outbox; COMMIT;
/// (5) Se commit falhar → compensar deletando tenant de identidade → TA-ERR-010.
///
/// Nunca deixa estado parcial (PBT-07).
/// </summary>
public sealed class TenantProvisioningSaga : ITenantProvisioningSaga
{
    private readonly TenantAdministrationDbContext _db;
    private readonly IIdentityTenantProvisioner _idp;
    private readonly IEventOutbox _outbox;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ILogger<TenantProvisioningSaga> _logger;

    /// <param name="db">DbContext para persistência.</param>
    /// <param name="idp">Adapter do Identity Platform.</param>
    /// <param name="outbox">Outbox para enfileirar eventos.</param>
    /// <param name="tenantContext">Contexto de correlação.</param>
    /// <param name="clock">Relógio injetado.</param>
    /// <param name="logger">Logger estruturado.</param>
    public TenantProvisioningSaga(
        TenantAdministrationDbContext db,
        IIdentityTenantProvisioner idp,
        IEventOutbox outbox,
        ITenantContext tenantContext,
        IClock clock,
        ILogger<TenantProvisioningSaga> logger)
    {
        _db = db;
        _idp = idp;
        _outbox = outbox;
        _tenantContext = tenantContext;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProvisioningResult> ExecuteAsync(
        Guid tenantId,
        string slug,
        string displayName,
        string timezone,
        string digestTime,
        string adminEmail,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        // ── (1) Verificar idempotência ────────────────────────────────────────
        var existing = await _db.ProvisioningRequests
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);

        if (existing?.Status == ProvisioningRequestStatus.Succeeded
            && existing.ResultTenantId.HasValue
            && existing.ResultIdentityTenantId is not null)
        {
            _logger.LogInformation(
                "Saga: requisição idempotente retornando resultado anterior. Key={Key} Slug={Slug}",
                idempotencyKey,
                slug);
            return new ProvisioningResult(existing.ResultTenantId.Value, existing.ResultIdentityTenantId);
        }

        // Registrar requisição em progresso
        var request = new TenantProvisioningRequest
        {
            IdempotencyKey = idempotencyKey,
            Slug = slug,
            Status = ProvisioningRequestStatus.InProgress
        };
        await _db.ProvisioningRequests.AddAsync(request, ct);
        await _db.SaveChangesAsync(ct);

        // ── (2) Criar no IdP ─────────────────────────────────────────────────
        _logger.LogInformation(
            "Saga: passo 1 — criar tenant no IdP. Slug={Slug} CorrelationId={CorrelationId}",
            slug,
            _tenantContext.CorrelationId);

        string identityTenantId;
        try
        {
            var idpResult = await _idp.CreateTenantAsync(slug, adminEmail, idempotencyKey, ct);
            identityTenantId = idpResult.IdentityTenantId;
        }
        catch (Exception ex)
        {
            request.Status = ProvisioningRequestStatus.Failed;
            await _db.SaveChangesAsync(ct);

            _logger.LogError(
                ex,
                "Saga: falha no IdP — nenhum tenant criado. Slug={Slug} CorrelationId={CorrelationId}",
                slug,
                _tenantContext.CorrelationId);

            throw new InvalidOperationException(
                $"TA-ERR-009: Falha ao criar tenant de identidade para slug '{slug}'.", ex);
        }

        // ── (3) Persistir agregado + Outbox ──────────────────────────────────
        _logger.LogInformation(
            "Saga: passo 2 — persistir agregado. Slug={Slug} CorrelationId={CorrelationId}",
            slug,
            _tenantContext.CorrelationId);

        try
        {
            var slugResult = Slug.Create(slug);
            if (slugResult.IsFailure)
                throw new InvalidOperationException(
                    $"TA-ERR-011: Slug inválido '{slug}' — {slugResult.ErrorMessage}");

            var timezoneResult = TimezoneIana.Create(timezone);
            if (timezoneResult.IsFailure)
                throw new InvalidOperationException(
                    $"TA-ERR-012: Timezone inválido '{timezone}' — {timezoneResult.ErrorMessage}");

            var digestTimeResult = Domain.ValueObjects.DigestTime.Create(digestTime);
            if (digestTimeResult.IsFailure)
                throw new InvalidOperationException(
                    $"TA-ERR-013: DigestTime inválido '{digestTime}' — {digestTimeResult.ErrorMessage}");

            var slugVo = slugResult.Value;
            var timezoneVo = timezoneResult.Value;
            var digestTimeVo = digestTimeResult.Value;

            var tenant = Tenant.Provision(slugVo, displayName, timezoneVo, digestTimeVo, adminEmail, _clock.UtcNow);
            tenant.SetIdentityTenantId(identityTenantId);

            await _db.Tenants.AddAsync(tenant, ct);

            // Outbox: append de todos os domain events do agregado
            foreach (var domainEvent in tenant.DomainEvents)
                await _outbox.AppendAsync(domainEvent, ct);

            await _db.SaveChangesAsync(ct);

            request.Status = ProvisioningRequestStatus.Succeeded;
            request.ResultTenantId = tenant.Id;
            request.ResultIdentityTenantId = identityTenantId;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Saga: concluída com sucesso. TenantId={TenantId} Slug={Slug} CorrelationId={CorrelationId}",
                tenant.Id,
                slug,
                _tenantContext.CorrelationId);

            return new ProvisioningResult(tenant.Id, identityTenantId);
        }
        catch (Exception ex)
        {
            // ── (4) Compensação: deletar tenant do IdP ────────────────────────
            _logger.LogError(
                ex,
                "Saga: falha na persistência — iniciando compensação. " +
                "Slug={Slug} IdentityTenantId={IdpId} CorrelationId={CorrelationId}",
                slug,
                identityTenantId,
                _tenantContext.CorrelationId);

            try
            {
                await _idp.DeleteTenantAsync(identityTenantId, ct);
                _logger.LogInformation(
                    "Saga: compensação bem-sucedida — tenant removido do IdP. IdpId={IdpId}",
                    identityTenantId);
            }
            catch (Exception compensationEx)
            {
                _logger.LogCritical(
                    compensationEx,
                    "Saga: FALHA NA COMPENSAÇÃO — tenant {IdpId} pode existir no IdP sem registro no banco. " +
                    "Intervenção manual necessária. Slug={Slug} CorrelationId={CorrelationId}",
                    identityTenantId,
                    slug,
                    _tenantContext.CorrelationId);
            }

            request.Status = ProvisioningRequestStatus.Failed;
            try { await _db.SaveChangesAsync(ct); } catch { /* best-effort */ }

            throw new InvalidOperationException(
                $"TA-ERR-010: Provisionamento revertido por falha de persistência para slug '{slug}'.", ex);
        }
    }
}
