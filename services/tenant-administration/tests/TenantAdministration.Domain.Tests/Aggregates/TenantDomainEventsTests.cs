using FluentAssertions;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.Events;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Domain.Tests.Aggregates;

/// <summary>
/// Testes de enfileiramento de domain events pelo agregado Tenant.
/// Cobre TASK-06: cada operação enfileira exatamente o evento correto.
/// </summary>
public sealed class TenantDomainEventsTests
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 6, 13, 0, 0, 0, TimeSpan.Zero);

    private static Tenant ProvisionValid(string slug = "test-tenant") =>
        Tenant.Provision(
            Slug.Create(slug).Value,
            "Test Tenant",
            TimezoneIana.Default,
            DigestTime.Default,
            "admin@test.com",
            FrozenNow);

    // ──────────────────────────────────────────────
    // TenantProvisioned
    // ──────────────────────────────────────────────

    [Fact]
    public void Provision_EnqueuesTenantProvisioned()
    {
        var tenant = ProvisionValid();

        tenant.DomainEvents.Should().HaveCount(1);
        var ev = tenant.DomainEvents.Single().Should().BeOfType<TenantProvisioned>().Subject;
        ev.TenantId.Should().Be(tenant.Id);
        ev.Slug.Should().Be("test-tenant");
        ev.DisplayName.Should().Be("Test Tenant");
        ev.Timezone.Should().Be("America/Sao_Paulo");
        ev.DigestTime.Should().Be("07:00");
        ev.OccurredAt.Should().Be(FrozenNow);
    }

    [Fact]
    public void Provision_TenantProvisioned_HasNonEmptyEventId()
    {
        var tenant = ProvisionValid();
        var ev = (TenantProvisioned)tenant.DomainEvents.Single();
        ev.EventId.Should().NotBe(Guid.Empty);
    }

    // ──────────────────────────────────────────────
    // TenantSuspended
    // ──────────────────────────────────────────────

    [Fact]
    public void Suspend_EnqueuesTenantSuspended()
    {
        var tenant = ProvisionValid();
        tenant.ClearDomainEvents();

        tenant.Suspend(FrozenNow);

        tenant.DomainEvents.Should().HaveCount(1);
        var ev = tenant.DomainEvents.Single().Should().BeOfType<TenantSuspended>().Subject;
        ev.TenantId.Should().Be(tenant.Id);
        ev.Slug.Should().Be("test-tenant");
        ev.OccurredAt.Should().Be(FrozenNow);
    }

    [Fact]
    public void Suspend_WhenFails_NoEventEnqueued()
    {
        var tenant = ProvisionValid();
        tenant.Suspend(FrozenNow);
        tenant.ClearDomainEvents();

        // Transição inválida — não deve enfileirar evento
        try { tenant.Suspend(FrozenNow); } catch (InvalidOperationException) { }

        tenant.DomainEvents.Should().BeEmpty(
            because: "operação que falha não deve enfileirar evento de domínio");
    }

    // ──────────────────────────────────────────────
    // TenantReactivated
    // ──────────────────────────────────────────────

    [Fact]
    public void Reactivate_EnqueuesTenantReactivated()
    {
        var tenant = ProvisionValid();
        tenant.Suspend(FrozenNow);
        tenant.ClearDomainEvents();

        tenant.Reactivate(FrozenNow);

        tenant.DomainEvents.Should().HaveCount(1);
        var ev = tenant.DomainEvents.Single().Should().BeOfType<TenantReactivated>().Subject;
        ev.TenantId.Should().Be(tenant.Id);
        ev.Slug.Should().Be("test-tenant");
        ev.OccurredAt.Should().Be(FrozenNow);
    }

    [Fact]
    public void Reactivate_WhenFails_NoEventEnqueued()
    {
        var tenant = ProvisionValid();
        tenant.ClearDomainEvents();

        // Tenant ativo — reativar deve falhar sem enfileirar evento
        try { tenant.Reactivate(FrozenNow); } catch (InvalidOperationException) { }

        tenant.DomainEvents.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // BrandingChanged
    // ──────────────────────────────────────────────

    [Fact]
    public void UpdateBranding_EnqueuesBrandingChanged()
    {
        var tenant = ProvisionValid();
        tenant.ClearDomainEvents();

        var theme = BrandingTheme.Create(
            "https://cdn/logo.svg",
            "https://cdn/fav.png",
            ColorPair.Create("#000000", "#FFFFFF").Value);

        tenant.UpdateBranding(theme, wcagContrastOk: true, contrastRatio: 21.00m, FrozenNow);

        tenant.DomainEvents.Should().HaveCount(1);
        var ev = tenant.DomainEvents.Single().Should().BeOfType<BrandingChanged>().Subject;
        ev.TenantId.Should().Be(tenant.Id);
        ev.Slug.Should().Be("test-tenant");
        ev.WcagContrastOk.Should().BeTrue();
        ev.OccurredAt.Should().Be(FrozenNow);
    }

    // ──────────────────────────────────────────────
    // DigestConfigChanged
    // ──────────────────────────────────────────────

    [Fact]
    public void UpdateDigestConfig_EnqueuesDigestConfigChanged()
    {
        var tenant = ProvisionValid();
        tenant.ClearDomainEvents();

        var newTimezone = TimezoneIana.Create("UTC").Value;
        var newDigestTime = DigestTime.Create("08:00").Value;

        tenant.UpdateDigestConfig(newTimezone, newDigestTime, FrozenNow);

        tenant.DomainEvents.Should().HaveCount(1);
        var ev = tenant.DomainEvents.Single().Should().BeOfType<DigestConfigChanged>().Subject;
        ev.TenantId.Should().Be(tenant.Id);
        ev.Timezone.Should().Be("UTC");
        ev.DigestTime.Should().Be("08:00");
    }

    // ──────────────────────────────────────────────
    // ClearDomainEvents
    // ──────────────────────────────────────────────

    [Fact]
    public void ClearDomainEvents_EmptiesQueue()
    {
        var tenant = ProvisionValid();
        tenant.DomainEvents.Should().HaveCount(1);

        tenant.ClearDomainEvents();

        tenant.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_IsReadOnly_CannotBeModifiedExternally()
    {
        var tenant = ProvisionValid();
        var events = tenant.DomainEvents;

        // IReadOnlyList não expõe Add/Remove
        events.Should().BeAssignableTo<IReadOnlyList<IDomainEvent>>();
        events.GetType().GetMethod("Add").Should().BeNull(
            because: "a lista de eventos não deve expor Add público");
    }

    // ──────────────────────────────────────────────
    // Imutabilidade dos tipos de evento
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(TenantProvisioned))]
    [InlineData(typeof(TenantSuspended))]
    [InlineData(typeof(TenantReactivated))]
    [InlineData(typeof(BrandingChanged))]
    [InlineData(typeof(DigestConfigChanged))]
    public void DomainEventTypes_AreImmutableRecords(Type eventType)
    {
        eventType.IsSealed.Should().BeTrue(because: "domain events devem ser sealed");
        // Records: propriedades com init setter ou sem setter
        foreach (var prop in eventType.GetProperties())
        {
            var setter = prop.SetMethod;
            if (setter is null) continue;
            var hasInitModifier = setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(m => m.FullName?.Contains("IsExternalInit") == true);
            var isPrivate = setter.IsPrivate;
            (hasInitModifier || isPrivate).Should().BeTrue(
                because: $"{eventType.Name}.{prop.Name} não deve ter setter público regular");
        }
    }

    // ──────────────────────────────────────────────
    // Sem dependências de infraestrutura nos eventos
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(TenantProvisioned))]
    [InlineData(typeof(TenantSuspended))]
    [InlineData(typeof(TenantReactivated))]
    [InlineData(typeof(BrandingChanged))]
    [InlineData(typeof(DigestConfigChanged))]
    public void DomainEventTypes_HaveNoDependencyOnInfrastructure(Type eventType)
    {
        var referencedAssemblies = eventType.Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .ToList();

        referencedAssemblies.Should().NotContain(
            name => name.Contains("EntityFramework") ||
                    name.Contains("Google.Cloud") ||
                    name.Contains("MassTransit") ||
                    name.Contains("AspNetCore"),
            because: "domain events não devem depender de infraestrutura");
    }
}
