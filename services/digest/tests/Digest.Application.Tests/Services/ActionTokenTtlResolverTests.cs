using Digest.Application.Options;
using Digest.Application.Services;
using Digest.Application.Tests.Stubs;
using FluentAssertions;
using Xunit;

namespace Digest.Application.Tests.Services;

/// <summary>
/// Testes unitários para <see cref="ActionTokenTtlResolver"/> (VAL-ACT-02).
/// Valida: usa setting do tenant quando presente; usa default quando ausente.
/// </summary>
public sealed class ActionTokenTtlResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static ActionTokenTtlResolver BuildResolver(
        InMemoryDigestTenantSettingsRepository settingsRepo,
        int defaultTtlHours = 48)
    {
        var options = new DigestOptions { DefaultActionTokenTtlHours = defaultTtlHours };
        return new ActionTokenTtlResolver(settingsRepo, options);
    }

    [Fact(DisplayName = "ResolveAsync usa setting do tenant quando configurado")]
    public async Task ResolveAsync_UsesTenantSetting_WhenPresent()
    {
        var settingsRepo = new InMemoryDigestTenantSettingsRepository();
        settingsRepo.SetTtlHours(TenantId, 72);

        var resolver = BuildResolver(settingsRepo, defaultTtlHours: 48);

        var ttl = await resolver.ResolveAsync(TenantId);

        ttl.Should().Be(TimeSpan.FromHours(72),
            because: "setting do tenant (72h) deve sobrepor o default global (VAL-ACT-02)");
    }

    [Fact(DisplayName = "ResolveAsync usa default global quando tenant não possui setting")]
    public async Task ResolveAsync_UsesDefault_WhenTenantSettingAbsent()
    {
        var settingsRepo = new InMemoryDigestTenantSettingsRepository();
        // Nenhum setting configurado para TenantId

        var resolver = BuildResolver(settingsRepo, defaultTtlHours: 48);

        var ttl = await resolver.ResolveAsync(TenantId);

        ttl.Should().Be(TimeSpan.FromHours(48),
            because: "ausência de setting do tenant deve usar o default de 48h (VAL-ACT-02)");
    }

    [Fact(DisplayName = "ResolveAsync respeita default customizado nas opções")]
    public async Task ResolveAsync_RespectsCustomDefaultInOptions()
    {
        var settingsRepo = new InMemoryDigestTenantSettingsRepository();

        var resolver = BuildResolver(settingsRepo, defaultTtlHours: 24);

        var ttl = await resolver.ResolveAsync(TenantId);

        ttl.Should().Be(TimeSpan.FromHours(24),
            because: "o default global pode ser alterado nas opções sem alterar o domínio");
    }

    [Fact(DisplayName = "ResolveAsync: setting do tenant sobrepõe qualquer default configurado")]
    public async Task ResolveAsync_TenantSettingOverridesAnyDefault()
    {
        var settingsRepo = new InMemoryDigestTenantSettingsRepository();
        settingsRepo.SetTtlHours(TenantId, 96);

        var resolver = BuildResolver(settingsRepo, defaultTtlHours: 24);

        var ttl = await resolver.ResolveAsync(TenantId);

        ttl.Should().Be(TimeSpan.FromHours(96),
            because: "setting do tenant deve sempre sobrepor o default (VAL-ACT-02)");
    }

    [Fact(DisplayName = "ResolveAsync retorna TimeSpan positivo sempre")]
    public async Task ResolveAsync_AlwaysReturnsPositiveTimeSpan()
    {
        var settingsRepo = new InMemoryDigestTenantSettingsRepository();
        settingsRepo.SetTtlHours(TenantId, 1); // mínimo razoável

        var resolver = BuildResolver(settingsRepo);

        var ttl = await resolver.ResolveAsync(TenantId);

        ttl.Should().BePositive(because: "TTL resolvido deve sempre ser positivo (VAL-ACT-02)");
    }
}
