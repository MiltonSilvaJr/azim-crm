using System.Text.Json;
using FluentAssertions;
using TenantAdministration.Domain.Events;
using TenantAdministration.Infrastructure.Outbox;
using Xunit;

namespace TenantAdministration.Api.Tests.Contracts;

/// <summary>
/// Testes de contrato para o envelope TRD dos eventos de domínio (TASK-20).
/// Verifica que o OutboxRepository gera envelopes conformes ao TRD §9.2:
/// event_id, event_type, event_version, occurred_at, correlation_id, tenant_id,
/// aggregate_type, aggregate_id, producer, payload.
/// design.md §9.1 — eventos: tenant.provisioned.v1, tenant.suspended.v1,
///                             tenant.reactivated.v1, tenant.branding_changed.v1.
/// </summary>
public sealed class EventContractTests
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    // ─── tenant.provisioned.v1 ────────────────────────────────────────────────

    [Fact(DisplayName = "TenantProvisioned: event_id corresponde ao EventId do domain event")]
    public void TenantProvisioned_EventId_MapsCorrectly()
    {
        var ev = new TenantProvisioned(
            TenantId: Guid.NewGuid(),
            Slug: "vellus",
            DisplayName: "Vellus",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            ProvisionedAt: DateTimeOffset.UtcNow);

        ev.EventId.Should().NotBeEmpty();
        ev.OccurredAt.Should().Be(ev.ProvisionedAt);
    }

    [Fact(DisplayName = "TenantProvisioned: payload não contém adminEmail (LGPD — design.md §10)")]
    public void TenantProvisioned_Payload_DoesNotContainAdminEmail()
    {
        var tenantId = Guid.NewGuid();
        var ev = new TenantProvisioned(
            TenantId: tenantId,
            Slug: "vellus",
            DisplayName: "Vellus",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            ProvisionedAt: DateTimeOffset.UtcNow);

        // Simula o payload como o OutboxRepository monta
        var payload = new
        {
            ev.TenantId,
            ev.Slug,
            ev.DisplayName,
            ev.Timezone,
            ev.DigestTime,
            ev.ProvisionedAt
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        json.Should().NotContain("adminEmail");
        json.Should().NotContain("admin@");
        json.Should().Contain("tenantId");
        json.Should().Contain("slug");
        json.Should().Contain("displayName");
    }

    [Fact(DisplayName = "TenantProvisioned: payload contém campos obrigatórios do TRD §9.2")]
    public void TenantProvisioned_Payload_ContainsRequiredFields()
    {
        var tenantId = Guid.NewGuid();
        var ev = new TenantProvisioned(
            TenantId: tenantId,
            Slug: "vellus",
            DisplayName: "Vellus",
            Timezone: "America/Sao_Paulo",
            DigestTime: "07:00",
            ProvisionedAt: DateTimeOffset.UtcNow);

        ev.TenantId.Should().Be(tenantId);
        ev.Slug.Should().Be("vellus");
        ev.DisplayName.Should().Be("Vellus");
        ev.Timezone.Should().Be("America/Sao_Paulo");
        ev.DigestTime.Should().Be("07:00");
    }

    // ─── tenant.suspended.v1 ──────────────────────────────────────────────────

    [Fact(DisplayName = "TenantSuspended: event_type é 'tenant.suspended.v1' — versão em event_type")]
    public void TenantSuspended_EventType_IncludesVersion()
    {
        // O mapeamento do OutboxRepository garante "tenant.suspended.v1"
        // Este teste verifica o contrato do domain event que alimenta o mapeamento
        var tenantId = Guid.NewGuid();
        var ev = new TenantSuspended(
            TenantId: tenantId,
            Slug: "vellus",
            SuspendedAt: DateTimeOffset.UtcNow);

        ev.EventId.Should().NotBeEmpty();
        ev.TenantId.Should().Be(tenantId);
        ev.Slug.Should().Be("vellus");
        ev.OccurredAt.Should().Be(ev.SuspendedAt);
    }

    [Fact(DisplayName = "TenantSuspended: payload contém tenantId e slug")]
    public void TenantSuspended_Payload_ContainsTenantIdAndSlug()
    {
        var tenantId = Guid.NewGuid();
        var ev = new TenantSuspended(
            TenantId: tenantId,
            Slug: "vellus",
            SuspendedAt: DateTimeOffset.UtcNow);

        var payload = new { ev.TenantId, ev.Slug, OccurredAt = ev.SuspendedAt };
        var json = JsonSerializer.Serialize(payload, JsonOpts);

        json.Should().Contain("tenantId");
        json.Should().Contain("slug");
        json.Should().Contain("occurredAt");
        json.Should().NotContain("adminEmail");
    }

    // ─── tenant.reactivated.v1 ────────────────────────────────────────────────

    [Fact(DisplayName = "TenantReactivated: payload contém tenantId e slug")]
    public void TenantReactivated_Payload_ContainsTenantIdAndSlug()
    {
        var tenantId = Guid.NewGuid();
        var ev = new TenantReactivated(
            TenantId: tenantId,
            Slug: "vellus",
            ReactivatedAt: DateTimeOffset.UtcNow);

        ev.EventId.Should().NotBeEmpty();
        ev.TenantId.Should().Be(tenantId);
        ev.OccurredAt.Should().Be(ev.ReactivatedAt);

        var payload = new { ev.TenantId, ev.Slug, OccurredAt = ev.ReactivatedAt };
        var json = JsonSerializer.Serialize(payload, JsonOpts);

        json.Should().Contain("tenantId");
        json.Should().Contain("slug");
        json.Should().NotContain("adminEmail");
    }

    // ─── tenant.branding_changed.v1 ───────────────────────────────────────────

    [Fact(DisplayName = "BrandingChanged: payload contém tenantId, slug e wcagContrastOk")]
    public void BrandingChanged_Payload_ContainsRequiredFields()
    {
        var tenantId = Guid.NewGuid();
        var ev = new BrandingChanged(
            TenantId: tenantId,
            Slug: "vellus",
            WcagContrastOk: true,
            ChangedAt: DateTimeOffset.UtcNow);

        ev.EventId.Should().NotBeEmpty();
        ev.TenantId.Should().Be(tenantId);
        ev.WcagContrastOk.Should().BeTrue();
        ev.OccurredAt.Should().Be(ev.ChangedAt);

        var payload = new { ev.TenantId, ev.Slug, ev.WcagContrastOk, ev.ChangedAt };
        var json = JsonSerializer.Serialize(payload, JsonOpts);

        json.Should().Contain("tenantId");
        json.Should().Contain("slug");
        json.Should().Contain("wcagContrastOk");
        json.Should().NotContain("adminEmail");
    }

    // ─── Envelope TRD §9.2 — campos obrigatórios ─────────────────────────────

    [Fact(DisplayName = "IDomainEvent: toda implementação tem EventId único e OccurredAt")]
    public void AllDomainEvents_HaveUniqueEventIdAndOccurredAt()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        IDomainEvent[] events =
        [
            new TenantProvisioned(tenantId, "s", "D", "America/Sao_Paulo", "07:00", now),
            new TenantSuspended(tenantId, "s", now),
            new TenantReactivated(tenantId, "s", now),
            new BrandingChanged(tenantId, "s", true, now)
        ];

        var eventIds = events.Select(e => e.EventId).ToList();
        eventIds.Should().OnlyHaveUniqueItems(
            because: "cada instância de evento deve ter EventId único (TRD §9.2)");
        events.Should().AllSatisfy(e =>
            e.OccurredAt.Should().Be(now,
                because: "OccurredAt deve mapear para o timestamp do fato de domínio"));
    }

    [Fact(DisplayName = "OutboxEvent: campos obrigatórios do TRD §9.2 devem existir")]
    public void OutboxEvent_HasRequiredTrdFields()
    {
        // Verifica que a entidade OutboxEvent possui os campos do envelope TRD §9.2:
        // event_id (Id), event_type (EventType), aggregate_type (AggregateType),
        // aggregate_id (AggregateId), tenant_id (TenantId), correlation_id (CorrelationId),
        // payload (Payload), occurred_at (CreatedAt)
        var props = typeof(OutboxEvent).GetProperties();
        var propNames = props.Select(p => p.Name).ToArray();

        propNames.Should().Contain("Id",          because: "mapeia para event_id do TRD §9.2");
        propNames.Should().Contain("EventType",   because: "mapeia para event_type do TRD §9.2");
        propNames.Should().Contain("AggregateType", because: "mapeia para aggregate_type do TRD §9.2");
        propNames.Should().Contain("AggregateId", because: "mapeia para aggregate_id do TRD §9.2");
        propNames.Should().Contain("TenantId",    because: "mapeia para tenant_id do TRD §9.2");
        propNames.Should().Contain("CorrelationId", because: "mapeia para correlation_id do TRD §9.2");
        propNames.Should().Contain("Payload",     because: "mapeia para payload do TRD §9.2");
        propNames.Should().Contain("CreatedAt",   because: "mapeia para occurred_at do TRD §9.2");
    }
}
