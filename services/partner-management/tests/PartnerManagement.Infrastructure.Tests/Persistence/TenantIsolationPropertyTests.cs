using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;
using PartnerManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Persistence;

/// <summary>
/// PBT-04 — Propriedade de isolamento por tenant (gate CI obrigatório, KPI-06).
/// Para qualquer par de tenants diferentes (A, B) e N parceiros no tenant A:
/// o tenant B nunca enxerga nenhum desses parceiros.
/// Gate CI: se este teste falhar, o pipeline rejeita o deploy (TASK-21, design §13).
/// Usa FsCheck com Testcontainers PostgreSQL real.
/// Mapeia: RNF 1, DD-001, ADR-0001, TASK-21.
/// </summary>
[Trait("Category", "TenantIsolation")]
public sealed class TenantIsolationPropertyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string _connectionString = null!;

    private static readonly ICanonicalRoleProvider RoleProvider = new AlwaysValidRoleProvider();
    private static readonly Guid ActorId = Guid.NewGuid();

    // Papéis canônicos para geração de parceiros
    private static readonly string[] CanonicalRoles = ["Indicador", "Revendedor", "Distribuidor", "Integrador"];

    public TenantIsolationPropertyTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("partner_management_pbt04_test")
            .WithUsername("app_test")
            .WithPassword("test_secret")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        using PartnerManagementDbContext ctx = CreateDbContext(Guid.NewGuid());
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    // =========================================================================
    // PBT-04: Para qualquer par (tenantA, tenantB) com tenantA ≠ tenantB,
    //         parceiros do tenant A não são visíveis pelo tenant B.
    // =========================================================================

    /// <summary>
    /// PBT-04: Propriedade de isolamento cross-tenant verificada com FsCheck.
    /// Para qualquer N ∈ [1..5] parceiros no tenant A, o tenant B retorna lista vazia.
    /// </summary>
    /// <summary>
    /// PBT-04: FsCheck injeta <paramref name="partnerCount"/> via <see cref="PartnerCountArb"/>.
    /// Para cada contagem gerada, verifica que tenant B não vê nenhum parceiro do tenant A.
    /// </summary>
    [Property(MaxTest = 10, Arbitrary = new[] { typeof(PartnerCountArb) },
        DisplayName = "PBT-04: parceiros de tenant A nunca são visíveis pelo tenant B")]
    public bool TenantA_Partners_AreNeverVisibleByTenantB(int partnerCount)
    {
        return RunIsolationCheck(partnerCount).GetAwaiter().GetResult();
    }

    private async Task<bool> RunIsolationCheck(int partnerCount)
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        // Cria N parceiros no tenant A
        List<Guid> createdIds = [];

        await using (PartnerManagementDbContext ctxA = CreateDbContext(tenantA))
        {
            PartnerRepository repoA = new(ctxA);

            for (int i = 0; i < partnerCount; i++)
            {
                string role = CanonicalRoles[i % CanonicalRoles.Length];
                Partner partner = Partner.Create(
                    tenantA,
                    name: $"Parceiro PBT {Guid.NewGuid():N}",
                    role: role,
                    commissionDefaults: CommissionDefaults.Create(Percentage.Create(0m), Percentage.Create(0m)),
                    contact: null,
                    notes: null,
                    roleProvider: RoleProvider,
                    createdBy: ActorId);

                await repoA.AddAsync(partner);
                createdIds.Add(partner.Id);
            }

            await ctxA.SaveChangesAsync();
        }

        // Verifica: tenant B não enxerga nenhum dos parceiros do tenant A
        await using PartnerManagementDbContext ctxB = CreateDbContext(tenantB);
        PartnerRepository repoB = new(ctxB);

        foreach (Guid partnerId in createdIds)
        {
            Partner? found = await repoB.GetByIdAsync(partnerId);

            if (found is not null)
            {
                // Violação de isolamento — propriedade falhou
                return false;
            }
        }

        // Verifica também via ListAsync que tenant B não lista parceiros do tenant A
        (IReadOnlyList<Partner> listed, _) = await repoB.ListAsync(
            active: null, triagePending: false, page: 1, pageSize: 100);

        bool leak = listed.Any(p => createdIds.Contains(p.Id));
        return !leak;
    }

    // =========================================================================
    // PBT-04b: Propriedade inversa — tenant A sempre enxerga seus próprios parceiros.
    // =========================================================================

    /// <summary>
    /// PBT-04b: Parceiros do tenant A devem ser visíveis pelo próprio tenant A (não há falso negativo).
    /// </summary>
    [Property(MaxTest = 10, Arbitrary = new[] { typeof(PartnerCountArb) },
        DisplayName = "PBT-04b: tenant A sempre enxerga seus próprios parceiros")]
    public bool TenantA_Partners_AreAlwaysVisibleByTenantA(int partnerCount)
    {
        return RunVisibilityCheck(partnerCount).GetAwaiter().GetResult();
    }

    private async Task<bool> RunVisibilityCheck(int partnerCount)
    {
        Guid tenantId = Guid.NewGuid();
        List<Guid> createdIds = [];

        await using (PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId))
        {
            PartnerRepository repo = new(ctxWrite);

            for (int i = 0; i < partnerCount; i++)
            {
                string role = CanonicalRoles[i % CanonicalRoles.Length];
                Partner partner = Partner.Create(
                    tenantId,
                    name: $"PBT Visível {Guid.NewGuid():N}",
                    role: role,
                    commissionDefaults: CommissionDefaults.Create(Percentage.Create(0m), Percentage.Create(0m)),
                    contact: null,
                    notes: null,
                    roleProvider: RoleProvider,
                    createdBy: ActorId);

                await repo.AddAsync(partner);
                createdIds.Add(partner.Id);
            }

            await ctxWrite.SaveChangesAsync();
        }

        // Verifica: tenant lê todos os próprios parceiros
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        PartnerRepository repoRead = new(ctxRead);

        foreach (Guid partnerId in createdIds)
        {
            Partner? found = await repoRead.GetByIdAsync(partnerId);
            if (found is null)
            {
                return false; // Falso negativo — violação
            }
        }

        return true;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private PartnerManagementDbContext CreateDbContext(Guid tenantId)
    {
        DbContextOptions<PartnerManagementDbContext> options = new DbContextOptionsBuilder<PartnerManagementDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new PartnerManagementDbContext(options, new StaticTenantContext(tenantId));
    }

    private sealed class AlwaysValidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => true;
    }

    private sealed class StaticTenantContext : Application.Ports.ITenantContext
    {
        public StaticTenantContext(Guid tenantId) => CurrentTenantId = tenantId;
        public Guid CurrentTenantId { get; }
    }

}

/// <summary>
/// Gerador FsCheck 3.x para contagem de parceiros no PBT-04.
/// Usa FsCheck.Fluent.Gen e .ToArbitrary() (API FsCheck 3.x).
/// </summary>
public static class PartnerCountArb
{
    /// <summary>Gera número de parceiros entre 1 e 3 (reduzido para Testcontainers).</summary>
    public static Arbitrary<int> Generate() =>
        Gen.Choose(1, 3).ToArbitrary();
}
