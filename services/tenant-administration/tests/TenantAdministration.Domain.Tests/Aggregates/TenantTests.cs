using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.Entities;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.Aggregates;

/// <summary>
/// Testes para o agregado Tenant e sua state machine.
/// Cobre TASK-05: PBT-08 — transições válidas.
/// </summary>
public sealed class TenantTests
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 6, 13, 0, 0, 0, TimeSpan.Zero);

    private static Tenant ProvisionValid(string slug = "my-tenant") =>
        Tenant.Provision(
            Slug.Create(slug).Value,
            "My Tenant Inc.",
            TimezoneIana.Default,
            DigestTime.Default,
            "admin@mytenant.com",
            FrozenNow);

    // ──────────────────────────────────────────────
    // Provision
    // ──────────────────────────────────────────────

    [Fact]
    public void Provision_WithValidFields_CreatesInProvisionedStatus()
    {
        var tenant = ProvisionValid();
        tenant.Status.Should().Be(TenantStatus.Provisioned);
        tenant.Slug.Value.Should().Be("my-tenant");
        tenant.DisplayName.Should().Be("My Tenant Inc.");
        tenant.Timezone.Should().Be(TimezoneIana.Default);
        tenant.DigestTime.Should().Be(DigestTime.Default);
        tenant.ProvisionedAt.Should().Be(FrozenNow);
    }

    [Fact]
    public void Provision_Slug_HasNoPublicSetter()
    {
        var type = typeof(Tenant);
        var slugProp = type.GetProperty(nameof(Tenant.Slug));
        slugProp.Should().NotBeNull();
        // Slug pode ter setter privado (EF Core) mas nunca setter público
        var setter = slugProp!.SetMethod;
        var isPublicSetter = setter is not null && setter.IsPublic;
        isPublicSetter.Should().BeFalse(because: "Slug não deve ter setter público — é imutável após Provision");
    }

    // ──────────────────────────────────────────────
    // Suspend
    // ──────────────────────────────────────────────

    [Fact]
    public void Suspend_WhenProvisioned_TransitionsToSuspended()
    {
        var tenant = ProvisionValid();
        tenant.ClearDomainEvents();
        tenant.Suspend(FrozenNow);
        tenant.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact]
    public void Suspend_WhenAlreadySuspended_ThrowsInvalidTransition()
    {
        var tenant = ProvisionValid();
        tenant.Suspend(FrozenNow);

        var act = () => tenant.Suspend(FrozenNow);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TA-ERR-007*");
    }

    // ──────────────────────────────────────────────
    // Reactivate
    // ──────────────────────────────────────────────

    [Fact]
    public void Reactivate_WhenSuspended_TransitionsToProvisioned()
    {
        var tenant = ProvisionValid();
        tenant.Suspend(FrozenNow);
        tenant.ClearDomainEvents();
        tenant.Reactivate(FrozenNow);
        tenant.Status.Should().Be(TenantStatus.Provisioned);
    }

    [Fact]
    public void Reactivate_WhenAlreadyProvisioned_ThrowsInvalidTransition()
    {
        var tenant = ProvisionValid();

        var act = () => tenant.Reactivate(FrozenNow);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TA-ERR-007*");
    }

    // ──────────────────────────────────────────────
    // UpdateBranding
    // ──────────────────────────────────────────────

    [Fact]
    public void UpdateBranding_WithValidThemeAndWcagApproved_UpdatesBranding()
    {
        var tenant = ProvisionValid();
        tenant.ClearDomainEvents();

        var theme = BrandingTheme.Create(
            "https://cdn/logo.svg",
            "https://cdn/fav.png",
            ColorPair.Create("#000000", "#FFFFFF").Value);

        tenant.UpdateBranding(theme, wcagContrastOk: true, contrastRatio: 21.00m, FrozenNow);

        tenant.Branding.Should().NotBeNull();
        tenant.Branding!.Theme.LogoUrl.Should().Be("https://cdn/logo.svg");
        tenant.Branding.WcagContrastOk.Should().BeTrue();
    }

    [Fact]
    public void UpdateBranding_OnlyAccessibleViaTenantMethod()
    {
        // TenantBranding não deve ter método próprio de atualização
        var brandingType = typeof(TenantBranding);
        brandingType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == brandingType)
            .Should().BeEmpty(because: "TenantBranding não deve ter comportamento público próprio");
    }

    // ──────────────────────────────────────────────
    // UpdateDigestConfig
    // ──────────────────────────────────────────────

    [Fact]
    public void UpdateDigestConfig_WithValidValues_UpdatesConfig()
    {
        var tenant = ProvisionValid();
        tenant.ClearDomainEvents();

        var newTimezone = TimezoneIana.Create("UTC").Value;
        var newDigestTime = DigestTime.Create("08:00").Value;

        tenant.UpdateDigestConfig(newTimezone, newDigestTime, FrozenNow);

        tenant.Timezone.Should().Be(newTimezone);
        tenant.DigestTime.Should().Be(newDigestTime);
    }

    // ──────────────────────────────────────────────
    // PBT-08 — transições válidas do estado do tenant
    // ──────────────────────────────────────────────

    /// <summary>
    /// PBT-08: Sequências arbitrárias de Suspend/Reactivate.
    /// Apenas transições canônicas (Provisioned→Suspended e Suspended→Provisioned) passam.
    /// Tentativas de transição inválida lançam exceção e não alteram o estado.
    /// </summary>
    [Property(MaxTest = 300, DisplayName = "PBT-08: Apenas transições canônicas de estado")]
    public Property Pbt08_OnlyCanonicalStateTransitions()
    {
        // Gera sequências de 0/1 onde 0=Suspend, 1=Reactivate
        var gen = Gen.Choose(1, 20)
            .SelectMany(len => Gen.Elements(0, 1).ArrayOf(len));

        return Prop.ForAll(Arb.From(gen), ops =>
        {
            var tenant = ProvisionValid("pbt-tenant");
            tenant.ClearDomainEvents();

            foreach (var op in ops)
            {
                var statusBefore = tenant.Status;

                if (op == 0)
                {
                    // Suspend
                    if (statusBefore == TenantStatus.Provisioned)
                    {
                        tenant.Suspend(FrozenNow);
                        tenant.Status.Should().Be(TenantStatus.Suspended);
                    }
                    else
                    {
                        // Deve lançar e não alterar estado
                        var act = () => tenant.Suspend(FrozenNow);
                        act.Should().Throw<InvalidOperationException>();
                        tenant.Status.Should().Be(statusBefore);
                    }
                }
                else
                {
                    // Reactivate
                    if (statusBefore == TenantStatus.Suspended)
                    {
                        tenant.Reactivate(FrozenNow);
                        tenant.Status.Should().Be(TenantStatus.Provisioned);
                    }
                    else
                    {
                        var act = () => tenant.Reactivate(FrozenNow);
                        act.Should().Throw<InvalidOperationException>();
                        tenant.Status.Should().Be(statusBefore);
                    }
                }
            }

            return true;
        });
    }
}
