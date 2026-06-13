using FluentAssertions;
using NSubstitute;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Application.Queries;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Application.Tests.Queries;

public sealed class GetPublicBrandJsonHandlerTests
{
    private readonly ITenantRepository _repository = Substitute.For<ITenantRepository>();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private GetPublicBrandJsonHandler CreateHandler() => new(_repository);

    private Tenant CreateTenantWithBranding()
    {
        var slug = Slug.Create("meu-tenant").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var tenant = Tenant.Provision(slug, "Meu Tenant", tz, DigestTime.Default, "admin@secret.com", Now);
        tenant.ClearDomainEvents();

        var colors = ColorPair.Create("#000000", "#FFFFFF").Value;
        var theme = BrandingTheme.Create("https://cdn/logo.png", "https://cdn/favicon.ico", colors);
        tenant.UpdateBranding(theme, true, 21.0m, Now);
        tenant.ClearDomainEvents();
        return tenant;
    }

    // ──────────────────────────────────────────────────────────────
    // Cenário de sucesso
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Slug existente ativo: retorna brand.json com DerivedTones")]
    public async Task ActiveTenant_ShouldReturnBrandJson()
    {
        var tenant = CreateTenantWithBranding();
        _repository.FindBySlugAsync("meu-tenant", Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(
            new GetPublicBrandJsonQuery("meu-tenant"), CancellationToken.None);

        result.Slug.Should().Be("meu-tenant");
        result.LogoUrl.Should().Be("https://cdn/logo.png");
        result.WcagContrastOk.Should().BeTrue();
        result.DerivedTones.Should().NotBeNull();
        result.Colors.Should().NotBeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // brand.json não expõe dados sensíveis
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "brand.json não contém adminEmail nem identityTenantId")]
    public async Task BrandJson_ShouldNot_ContainSensitiveData()
    {
        var tenant = CreateTenantWithBranding();
        _repository.FindBySlugAsync("meu-tenant", Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await CreateHandler().Handle(
            new GetPublicBrandJsonQuery("meu-tenant"), CancellationToken.None);

        // PublicBrandJsonDto não tem propriedade AdminEmail ou IdentityTenantId
        var props = typeof(PublicBrandJsonDto).GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();

        props.Should().NotContain("adminemail");
        props.Should().NotContain("identitytenantid");
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-008: slug inexistente (anti-enumeração)
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TA-ERR-008: slug inexistente → sem revelar se suspenso (anti-enumeração)")]
    public async Task NonExistentSlug_ShouldThrow_TA_ERR_008()
    {
        _repository.FindBySlugAsync("inexistente", Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var act = () => CreateHandler().Handle(
            new GetPublicBrandJsonQuery("inexistente"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-008");
        ex.Which.Message.Should().NotContain("suspenso", "não deve revelar estado do tenant");
    }

    // ──────────────────────────────────────────────────────────────
    // Tenant suspenso → mesmo 404 (anti-enumeração)
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Tenant suspenso: mesmo TA-ERR-008 sem distinção (anti-enumeração)")]
    public async Task SuspendedTenant_ShouldReturn_TA_ERR_008()
    {
        var slug = Slug.Create("meu-tenant").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var tenant = Tenant.Provision(slug, "Meu Tenant", tz, DigestTime.Default, "admin@a.com", Now);
        tenant.Suspend(Now);
        tenant.ClearDomainEvents();
        _repository.FindBySlugAsync("meu-tenant", Arg.Any<CancellationToken>()).Returns(tenant);

        var act = () => CreateHandler().Handle(
            new GetPublicBrandJsonQuery("meu-tenant"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-008");
    }
}
