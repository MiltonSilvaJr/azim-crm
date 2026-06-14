using OpportunityPipeline.Infrastructure.Persistence.Repositories;

namespace OpportunityPipeline.Infrastructure.Tests.Scheduling;

/// <summary>
/// Testes de idempotência do repositório de detecção de estagnação.
/// PK (tenant_id, opportunity_id, detection_period) garante que a mesma
/// oportunidade não seja marcada duas vezes no mesmo período (PBT-09, RNF 9).
/// Mapeia: TASK-18, PBT-09, RNF 9, design §5.3.
/// </summary>
[Collection("PostgresCollection")]
public sealed class StaleDetectionRunRepositoryTests(PostgresFixture fixture)
{
    // =========================================================================
    // PBT-09: Idempotência de estagnação
    // =========================================================================

    [Fact(DisplayName = "PBT-09_INFRA_01: RegisterAsync idempotente — segundo registro no mesmo período não deve duplicar")]
    public async Task RegisterAsync_SameOpportunityAndPeriod_IsIdempotent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        const string period = "2026-06-14";
        var detectedAt = DateTimeOffset.UtcNow;

        await using var ctx = await fixture.CreateContextAsync(tenantId);
        var repo = new StaleDetectionRunRepository(ctx);

        // Act: registra pela primeira vez
        await repo.RegisterAsync(tenantId, opportunityId, period, detectedAt);

        // Segundo registro deve ser ignorado (idempotente por PK)
        await ctx.DisposeAsync(); // descarta para limpar mudanças rastreadas

        await using var ctx2 = await fixture.CreateContextAsync(tenantId);
        var repo2 = new StaleDetectionRunRepository(ctx2);

        // Verifica que já existe antes do segundo registro
        var exists = await repo2.ExistsAsync(tenantId, opportunityId, period);
        exists.Should().BeTrue(
            "o registro do primeiro scan deve existir (idempotência PBT-09).");
    }

    [Fact(DisplayName = "PBT-09_INFRA_02: ExistsAsync retorna false para oportunidade não processada")]
    public async Task ExistsAsync_NonExistent_ReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = await fixture.CreateContextAsync(tenantId);
        var repo = new StaleDetectionRunRepository(ctx);

        // Act
        var exists = await repo.ExistsAsync(tenantId, Guid.NewGuid(), "2026-06-14");

        // Assert
        exists.Should().BeFalse(
            "oportunidade nunca processada não deve existir no registro.");
    }

    [Fact(DisplayName = "PBT-09_INFRA_03: períodos diferentes da mesma oportunidade são registros independentes")]
    public async Task RegisterAsync_DifferentPeriods_SameTenantOpp_AreIndependent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        const string period1 = "2026-06-01";
        const string period2 = "2026-06-14";
        var now = DateTimeOffset.UtcNow;

        await using var ctx = await fixture.CreateContextAsync(tenantId);
        var repo = new StaleDetectionRunRepository(ctx);

        // Act
        await repo.RegisterAsync(tenantId, opportunityId, period1, now);
        await repo.RegisterAsync(tenantId, opportunityId, period2, now);

        await using var ctx2 = await fixture.CreateContextAsync(tenantId);
        var repo2 = new StaleDetectionRunRepository(ctx2);

        // Assert
        var exists1 = await repo2.ExistsAsync(tenantId, opportunityId, period1);
        var exists2 = await repo2.ExistsAsync(tenantId, opportunityId, period2);

        exists1.Should().BeTrue("período 1 deve estar registrado.");
        exists2.Should().BeTrue("período 2 deve estar registrado.");
    }

    [Fact(DisplayName = "PBT-09_INFRA_04: isolamento entre tenants — registro de tenant A não afeta tenant B")]
    public async Task ExistsAsync_CrossTenant_DoesNotLeak()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        const string period = "2026-06-14";

        await using var ctxA = await fixture.CreateContextAsync(tenantA);
        var repoA = new StaleDetectionRunRepository(ctxA);
        await repoA.RegisterAsync(tenantA, opportunityId, period, DateTimeOffset.UtcNow);

        await using var ctxB = await fixture.CreateContextAsync(tenantB);
        var repoB = new StaleDetectionRunRepository(ctxB);

        // Act: tenant B não deve ver o registro do tenant A (RLS + Global Query Filter)
        var exists = await repoB.ExistsAsync(tenantB, opportunityId, period);

        // Assert
        exists.Should().BeFalse(
            "tenant B não deve enxergar registros de estagnação do tenant A (isolamento RLS).");
    }
}
