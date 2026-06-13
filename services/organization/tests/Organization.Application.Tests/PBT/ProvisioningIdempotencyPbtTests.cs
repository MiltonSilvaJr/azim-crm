using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using Organization.Application.Commands.Provisioning;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Xunit;

namespace Organization.Application.Tests.PBT;

/// <summary>
/// PBT-03 (provisionamento) — Idempotência do provisionamento inicial da organização.
/// Propriedade: N reprocessamentos do mesmo <c>MessageId</c> produzem estado final idêntico,
/// sem duplicar BUs, usuários ou eventos.
/// </summary>
public sealed class ProvisioningIdempotencyPbtTests
{
    // ── PBT-03-A: N repetições do mesmo messageId não duplicam entidades ─────────────

    [Property(MaxTest = 100)]
    public Property ProvisionNTimes_SameMessageId_ProducesSingleEntity(PositiveInt repetitions)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(repetitions)),
            r =>
            {
                // Arrange
                var tenantId = Guid.NewGuid();
                var messageId = Guid.NewGuid().ToString();
                var now = DateTimeOffset.UtcNow;
                var n = r.Get % 14 + 2; // entre 2 e 15 repetições

                var buRepo = Substitute.For<IBusinessUnitRepository>();
                var userRepo = Substitute.For<IUserRepository>();
                var identityProvisioner = Substitute.For<IIdentityProvisioner>();
                var inboxStore = Substitute.For<IInboxStore>();
                var outbox = Substitute.For<IEventOutbox>();
                var clock = Substitute.For<IClock>();
                var tenantContext = Substitute.For<ITenantContext>();

                clock.UtcNow.Returns(now);
                tenantContext.TenantId.Returns(tenantId);
                tenantContext.CorrelationId.Returns(Guid.NewGuid());
                tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
                identityProvisioner.ProvisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns("uid-idempotency");

                // Simula Inbox: primeira vez retorna false, demais true
                var callCount = 0;
                inboxStore.IsProcessedAsync(messageId, tenantId, Arg.Any<CancellationToken>())
                    .Returns(ci => callCount++ > 0);

                var handler = new ProvisionInitialOrganizationCommandHandler(
                    buRepo, userRepo, identityProvisioner, inboxStore, outbox, clock, tenantContext);

                var command = new ProvisionInitialOrganizationCommand(
                    messageId, tenantId, "admin@org.com", "Admin", "Organização");

                // Act — N execuções
                for (var i = 0; i < n; i++)
                {
                    handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();
                }

                // Assert — BU salva apenas 1 vez (na primeira execução)
                buRepo.Received(1).SaveAsync(Arg.Any<BusinessUnit>(), Arg.Any<CancellationToken>())
                      .GetAwaiter().GetResult();

                return true;
            });
    }

    // ── PBT-03-B: Primeira execução sempre cria exatamente 1 BU e 1 usuário ────────

    [Property(MaxTest = 100)]
    public Property FirstProvision_AlwaysCreatesExactlyOneBuAndOneUser(PositiveInt seed)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(seed)),
            _ =>
            {
                var tenantId = Guid.NewGuid();
                var messageId = Guid.NewGuid().ToString();

                var buRepo = Substitute.For<IBusinessUnitRepository>();
                var userRepo = Substitute.For<IUserRepository>();
                var identityProvisioner = Substitute.For<IIdentityProvisioner>();
                var inboxStore = Substitute.For<IInboxStore>();
                var outbox = Substitute.For<IEventOutbox>();
                var clock = Substitute.For<IClock>();
                var tenantContext = Substitute.For<ITenantContext>();

                clock.UtcNow.Returns(DateTimeOffset.UtcNow);
                tenantContext.TenantId.Returns(tenantId);
                tenantContext.CorrelationId.Returns(Guid.NewGuid());
                tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
                identityProvisioner.ProvisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns($"uid-{Guid.NewGuid()}");

                inboxStore.IsProcessedAsync(messageId, tenantId, Arg.Any<CancellationToken>())
                    .Returns(false);

                var buCount = 0;
                var userCount = 0;
                buRepo.When(r => r.SaveAsync(Arg.Any<BusinessUnit>(), Arg.Any<CancellationToken>()))
                    .Do(_ => buCount++);
                userRepo.When(r => r.SaveAsync(Arg.Any<Organization.Domain.Aggregates.User>(), Arg.Any<CancellationToken>()))
                    .Do(_ => userCount++);

                var handler = new ProvisionInitialOrganizationCommandHandler(
                    buRepo, userRepo, identityProvisioner, inboxStore, outbox, clock, tenantContext);

                var command = new ProvisionInitialOrganizationCommand(
                    messageId, tenantId, $"admin{seed.Get}@org.com", "Admin", "Organização");

                handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                return buCount == 1 && userCount == 1;
            });
    }
}
