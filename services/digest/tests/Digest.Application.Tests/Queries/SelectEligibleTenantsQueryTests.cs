using Digest.Application.Models;
using Digest.Application.Queries;
using Digest.Application.Tests.Stubs;
using NodaTime;
using Xunit;

namespace Digest.Application.Tests.Queries;

/// <summary>
/// Testes unitários de <see cref="SelectEligibleTenantsQueryHandler"/> (TASK-10).
/// Verifica elegibilidade por fuso IANA e dia útil (Req 2, design §5.2).
/// </summary>
public sealed class SelectEligibleTenantsQueryTests
{
    private readonly IDateTimeZoneProvider _zoneProvider = DateTimeZoneProviders.Tzdb;

    [Fact(DisplayName = "Tenant inativo nunca aparece como elegível")]
    public async Task InactiveTenant_NeverEligible()
    {
        var port = new InMemoryUserDirectoryPort();
        // 10:00 UTC → 07:00 America/Sao_Paulo (digest_time 07:00, segunda) — seria elegível se ativo
        port.AddTenant(new TenantInfo(
            Guid.NewGuid(), "America/Sao_Paulo", new TimeOnly(7, 0), Active: false));

        var handler = new SelectEligibleTenantsQueryHandler(port, _zoneProvider);
        // 2026-06-08 é segunda-feira; 10:00 UTC → 07:00 Sao_Paulo
        var referenceUtc = new DateTimeOffset(2026, 6, 8, 10, 0, 0, TimeSpan.Zero);
        var result = await handler.Handle(new SelectEligibleTenantsQuery(referenceUtc), default);

        Assert.Empty(result);
    }

    [Fact(DisplayName = "Tenant ativo com fuso America/Sao_Paulo às 10:00 UTC (07:00 local, segunda) é elegível")]
    public async Task ActiveTenant_SaoPaulo_10UTC_IsEligible()
    {
        var tenantId = Guid.NewGuid();
        var port = new InMemoryUserDirectoryPort();
        port.AddTenant(new TenantInfo(tenantId, "America/Sao_Paulo", new TimeOnly(7, 0), Active: true));

        var handler = new SelectEligibleTenantsQueryHandler(port, _zoneProvider);
        var referenceUtc = new DateTimeOffset(2026, 6, 8, 10, 0, 0, TimeSpan.Zero); // segunda
        var result = await handler.Handle(new SelectEligibleTenantsQuery(referenceUtc), default);

        Assert.Single(result);
        Assert.Equal(tenantId, result[0]);
    }

    [Fact(DisplayName = "Tenant com fuso America/New_York às 12:00 UTC (08:00 local, digest_time 08:00, terça) é elegível")]
    public async Task ActiveTenant_NewYork_12UTC_IsEligible()
    {
        var tenantId = Guid.NewGuid();
        var port = new InMemoryUserDirectoryPort();
        port.AddTenant(new TenantInfo(tenantId, "America/New_York", new TimeOnly(8, 0), Active: true));

        var handler = new SelectEligibleTenantsQueryHandler(port, _zoneProvider);
        // 2026-06-09 é terça-feira (UTC-4 em EDT); 12:00 UTC = 08:00 EDT
        var referenceUtc = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        var result = await handler.Handle(new SelectEligibleTenantsQuery(referenceUtc), default);

        Assert.Single(result);
        Assert.Equal(tenantId, result[0]);
    }

    [Fact(DisplayName = "Sábado no fuso do tenant não gera elegibilidade")]
    public async Task Saturday_NeverEligible()
    {
        var port = new InMemoryUserDirectoryPort();
        port.AddTenant(new TenantInfo(
            Guid.NewGuid(), "America/Sao_Paulo", new TimeOnly(7, 0), Active: true));

        var handler = new SelectEligibleTenantsQueryHandler(port, _zoneProvider);
        // 2026-06-13 é sábado; 10:00 UTC → 07:00 Sao_Paulo mas é sábado
        var referenceUtc = new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero);
        var result = await handler.Handle(new SelectEligibleTenantsQuery(referenceUtc), default);

        Assert.Empty(result);
    }

    [Fact(DisplayName = "Horário local diferente do digest_time não gera elegibilidade")]
    public async Task WrongTime_NeverEligible()
    {
        var port = new InMemoryUserDirectoryPort();
        port.AddTenant(new TenantInfo(
            Guid.NewGuid(), "America/Sao_Paulo", new TimeOnly(7, 0), Active: true));

        var handler = new SelectEligibleTenantsQueryHandler(port, _zoneProvider);
        // 11:00 UTC → 08:00 Sao_Paulo (não é 07:00)
        var referenceUtc = new DateTimeOffset(2026, 6, 8, 11, 0, 0, TimeSpan.Zero);
        var result = await handler.Handle(new SelectEligibleTenantsQuery(referenceUtc), default);

        Assert.Empty(result);
    }

    [Fact(DisplayName = "Múltiplos tenants — apenas os elegíveis são retornados")]
    public async Task MultipleTenants_OnlyEligibleReturned()
    {
        var eligibleId = Guid.NewGuid();
        var ineligibleId = Guid.NewGuid();
        var port = new InMemoryUserDirectoryPort();
        port.AddTenant(new TenantInfo(eligibleId, "America/Sao_Paulo", new TimeOnly(7, 0), Active: true));
        port.AddTenant(new TenantInfo(ineligibleId, "America/New_York", new TimeOnly(7, 0), Active: true));

        var handler = new SelectEligibleTenantsQueryHandler(port, _zoneProvider);
        // 10:00 UTC → 07:00 Sao_Paulo (elegível) | 06:00 New_York (não é 07:00)
        var referenceUtc = new DateTimeOffset(2026, 6, 8, 10, 0, 0, TimeSpan.Zero);
        var result = await handler.Handle(new SelectEligibleTenantsQuery(referenceUtc), default);

        Assert.Single(result);
        Assert.Contains(eligibleId, result);
        Assert.DoesNotContain(ineligibleId, result);
    }
}
