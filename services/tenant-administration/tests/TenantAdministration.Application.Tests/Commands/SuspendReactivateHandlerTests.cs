using FluentAssertions;
using NSubstitute;
using TenantAdministration.Application.Commands;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Application.Tests.Commands;

public sealed class SuspendReactivateHandlerTests
{
    private readonly ITenantRepository _repository = Substitute.For<ITenantRepository>();
    private readonly IEventOutbox _outbox = Substitute.For<IEventOutbox>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private Tenant CreateProvisionedTenant()
    {
        var slug = Slug.Create("meu-tenant").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        return Tenant.Provision(slug, "Meu Tenant", tz, DigestTime.Default, "admin@a.com", Now);
    }

    private Tenant CreateSuspendedTenant()
    {
        var tenant = CreateProvisionedTenant();
        tenant.Suspend(Now);
        tenant.ClearDomainEvents();
        return tenant;
    }

    // ──────────────────────────────────────────────────────────────
    // SuspendTenantHandler
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Suspend: tenant ativo → suspenso com TenantSuspended enfileirado")]
    public async Task Suspend_ActiveTenant_ShouldTransitionAndEnqueueEvent()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateProvisionedTenant();
        tenant.ClearDomainEvents();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var handler = new SuspendTenantHandler(_repository, _outbox, _clock);
        var result = await handler.Handle(new SuspendTenantCommand(tenant.Id), CancellationToken.None);

        result.Status.Should().Be("suspended");
        await _repository.Received(1).UpdateAsync(tenant, Arg.Any<CancellationToken>());
        await _outbox.Received(1).AppendRangeAsync(Arg.Any<IEnumerable<TenantAdministration.Domain.Events.IDomainEvent>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Suspend: tenant não encontrado → TA-ERR-008")]
    public async Task Suspend_TenantNotFound_ShouldThrow_TA_ERR_008()
    {
        _clock.UtcNow.Returns(Now);
        _repository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var handler = new SuspendTenantHandler(_repository, _outbox, _clock);
        var act = () => handler.Handle(new SuspendTenantCommand(Guid.NewGuid()), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-008");
    }

    [Fact(DisplayName = "Suspend: tenant já suspenso → TA-ERR-007 sem alterar estado")]
    public async Task Suspend_AlreadySuspended_ShouldThrow_TA_ERR_007()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateSuspendedTenant();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var handler = new SuspendTenantHandler(_repository, _outbox, _clock);
        var act = () => handler.Handle(new SuspendTenantCommand(tenant.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-007");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────
    // ReactivateTenantHandler
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Reactivate: tenant suspenso → ativo com TenantReactivated enfileirado")]
    public async Task Reactivate_SuspendedTenant_ShouldTransitionAndEnqueueEvent()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateSuspendedTenant();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var handler = new ReactivateTenantHandler(_repository, _outbox, _clock);
        var result = await handler.Handle(new ReactivateTenantCommand(tenant.Id), CancellationToken.None);

        result.Status.Should().Be("provisioned");
        await _repository.Received(1).UpdateAsync(tenant, Arg.Any<CancellationToken>());
        await _outbox.Received(1).AppendRangeAsync(Arg.Any<IEnumerable<TenantAdministration.Domain.Events.IDomainEvent>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Reactivate: tenant não encontrado → TA-ERR-008")]
    public async Task Reactivate_TenantNotFound_ShouldThrow_TA_ERR_008()
    {
        _clock.UtcNow.Returns(Now);
        _repository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var handler = new ReactivateTenantHandler(_repository, _outbox, _clock);
        var act = () => handler.Handle(new ReactivateTenantCommand(Guid.NewGuid()), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-008");
    }

    [Fact(DisplayName = "Reactivate: tenant já ativo (provisioned) → TA-ERR-007 sem alterar estado")]
    public async Task Reactivate_AlreadyActive_ShouldThrow_TA_ERR_007()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateProvisionedTenant();
        tenant.ClearDomainEvents();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var handler = new ReactivateTenantHandler(_repository, _outbox, _clock);
        var act = () => handler.Handle(new ReactivateTenantCommand(tenant.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-007");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
    }
}
