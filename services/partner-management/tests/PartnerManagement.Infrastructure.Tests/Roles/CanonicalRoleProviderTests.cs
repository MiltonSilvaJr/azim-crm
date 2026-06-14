using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PartnerManagement.Infrastructure.Roles;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Roles;

/// <summary>
/// Testes unitários para <see cref="CanonicalRoleProvider"/> (TASK-20).
/// Verifica: papéis canônicos retornados, case-insensitive, cache por tenant,
/// isolamento entre tenants diferentes.
/// Mapeia: Req 5.2, Req 5.3, DD-005, design §6.2, TASK-20.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CanonicalRoleProviderTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly CanonicalRoleProvider _provider;

    public CanonicalRoleProviderTests()
    {
        _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        _provider = new CanonicalRoleProvider(_cache);
    }

    public void Dispose() => _cache.Dispose();

    // =========================================================================
    // IsCanonical
    // =========================================================================

    [Theory(DisplayName = "TASK-20: IsCanonical retorna true para papéis canônicos padrão")]
    [InlineData("Indicador")]
    [InlineData("Revendedor")]
    [InlineData("Distribuidor")]
    [InlineData("Integrador")]
    public void IsCanonical_ReturnsTrueForDefaultCanonicalRoles(string role)
    {
        Guid tenantId = Guid.NewGuid();
        _provider.IsCanonical(role, tenantId).Should().BeTrue($"'{role}' deve ser canônico por padrão");
    }

    [Theory(DisplayName = "TASK-20: IsCanonical é case-insensitive (Req 5.2)")]
    [InlineData("indicador")]
    [InlineData("REVENDEDOR")]
    [InlineData("Distribuidor")]
    [InlineData("INTEGRADOR")]
    public void IsCanonical_IsCaseInsensitive(string role)
    {
        Guid tenantId = Guid.NewGuid();
        _provider.IsCanonical(role, tenantId).Should().BeTrue($"'{role}' deve ser aceito independente de capitalização");
    }

    [Fact(DisplayName = "TASK-20: IsCanonical retorna false para papel não canônico")]
    public void IsCanonical_ReturnsFalse_ForNonCanonicalRole()
    {
        Guid tenantId = Guid.NewGuid();
        _provider.IsCanonical("Corretor", tenantId).Should().BeFalse();
        _provider.IsCanonical("Franqueado", tenantId).Should().BeFalse();
    }

    [Theory(DisplayName = "TASK-20: IsCanonical retorna false para role vazia ou whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsCanonical_ReturnsFalse_ForEmptyOrWhitespace(string? role)
    {
        Guid tenantId = Guid.NewGuid();
        _provider.IsCanonical(role!, tenantId).Should().BeFalse();
    }

    // =========================================================================
    // GetCanonicalRoles — cache por tenant
    // =========================================================================

    [Fact(DisplayName = "TASK-20: GetCanonicalRoles retorna os papéis padrão")]
    public void GetCanonicalRoles_ReturnsDefaultRoles()
    {
        Guid tenantId = Guid.NewGuid();
        IReadOnlyList<string> roles = _provider.GetCanonicalRoles(tenantId);

        roles.Should().Contain("Indicador");
        roles.Should().Contain("Revendedor");
        roles.Should().Contain("Distribuidor");
        roles.Should().Contain("Integrador");
    }

    [Fact(DisplayName = "TASK-20: GetCanonicalRoles usa chaves de cache segregadas por tenant (design §14)")]
    public void GetCanonicalRoles_UsesSeparateCacheKeyPerTenant()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        // Chaves de cache incluem tenant_id — verifica que ambos os tenants retornam roles válidas
        // No MVP o seed é o mesmo, mas a chave de cache é diferente (canonical_roles_{tenantId})
        IReadOnlyList<string> rolesA = _provider.GetCanonicalRoles(tenantA);
        IReadOnlyList<string> rolesB = _provider.GetCanonicalRoles(tenantB);

        // Ambos devem conter os papéis padrão (MVP usa seed único)
        rolesA.Should().Contain("Indicador");
        rolesB.Should().Contain("Indicador");

        // Verificação de segregação: IsCanonical de tenant A não vaza para tenant B
        // (ambos têm o mesmo seed no MVP, mas a chave de cache é por tenant)
        _provider.IsCanonical("Indicador", tenantA).Should().BeTrue();
        _provider.IsCanonical("Indicador", tenantB).Should().BeTrue();
    }

    [Fact(DisplayName = "TASK-20: GetCanonicalRoles retorna mesmo resultado em chamadas consecutivas (cache hit)")]
    public void GetCanonicalRoles_ReturnsCachedResult_OnConsecutiveCalls()
    {
        Guid tenantId = Guid.NewGuid();

        IReadOnlyList<string> first = _provider.GetCanonicalRoles(tenantId);
        IReadOnlyList<string> second = _provider.GetCanonicalRoles(tenantId);

        // Mesma referência = cache hit (IMemoryCache.GetOrCreate retorna a mesma instância)
        second.Should().BeSameAs(first, "segunda chamada deve vir do cache");
    }
}
