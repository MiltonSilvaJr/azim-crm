using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Organization.Application.Commands.Invitation;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.PBT;

/// <summary>
/// PBT-03 (parcial) — Idempotência do aceite de convite.
/// Propriedade: reprocessar o mesmo token N vezes produz estado final idêntico,
/// sem duplicar usuários, memberships ou eventos.
/// </summary>
public sealed class AcceptInvitationIdempotencyTests
{
    [Property(MaxTest = 100)]
    public Property AcceptingInvitationNTimes_ProducesSameFinalState(PositiveInt repetitions)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var tokenHash = "hash-idempotencia";
        var n = Math.Min(repetitions.Get % 15 + 1, 15); // limita a 15 para performance

        var invitationRepo = Substitute.For<IUserInvitationRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var identityProvisioner = Substitute.For<IIdentityProvisioner>();
        var outbox = Substitute.For<IEventOutbox>();
        var tenantContext = Substitute.For<ITenantContext>();
        var clock = Substitute.For<IClock>();
        var tokenHasher = Substitute.For<ITokenHasher>();

        tenantContext.TenantId.Returns(tenantId);
        tenantContext.UserId.Returns(Guid.Empty);
        tenantContext.CorrelationId.Returns(Guid.NewGuid());
        tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
        clock.UtcNow.Returns(now);
        tokenHasher.Hash("plain-token").Returns(tokenHash);
        identityProvisioner.ProvisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("uid-estavel");

        var invitation = UserInvitation.Create(
            "user@example.com",
            InvitationToken.FromHash(tokenHash),
            tenantId,
            [(buId, Role.Vendedor)],
            now.AddHours(72),
            now.AddMinutes(-10));

        // Estado do "banco" simulado
        User? persistedUser = null;
        UserInvitation? persistedInvitation = invitation;

        invitationRepo.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<UserInvitation?>(persistedInvitation));
        invitationRepo.IsEmailActiveUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        invitationRepo.SaveAsync(Arg.Do<UserInvitation>(inv => persistedInvitation = inv), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(persistedUser));
        userRepo.SaveAsync(Arg.Do<User>(u => persistedUser = u), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new AcceptInvitationCommandHandler(
            invitationRepo, userRepo, identityProvisioner, outbox, tenantContext, clock, tokenHasher);

        // Act — N reprocessamentos do mesmo token
        Guid? firstUserId = null;
        for (var i = 0; i < n; i++)
        {
            var userId = handler.Handle(
                new AcceptInvitationCommand("plain-token", "Nome Usuário"),
                CancellationToken.None).GetAwaiter().GetResult();

            if (firstUserId is null)
                firstUserId = userId;
        }

        // Propriedade: apenas um usuário criado, não N
        var createCalls = userRepo.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == "SaveAsync");

        return (createCalls == 1)
            .ToProperty()
            .Label($"usuário deve ser criado exatamente uma vez; criado {createCalls} vezes em {n} reprocessamentos");
    }

    /// <summary>
    /// PBT-03 (complementar): estado do convite após N aceites permanece <c>Accepted</c>.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvitationState_RemainsAccepted_AfterMultipleAccepts(PositiveInt repetitions)
    {
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var tokenHash = "hash-estado-aceito";
        var n = Math.Min(repetitions.Get % 10 + 1, 10);

        var invitationRepo = Substitute.For<IUserInvitationRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var identityProvisioner = Substitute.For<IIdentityProvisioner>();
        var outbox = Substitute.For<IEventOutbox>();
        var tenantContext = Substitute.For<ITenantContext>();
        var clock = Substitute.For<IClock>();
        var tokenHasher = Substitute.For<ITokenHasher>();

        tenantContext.TenantId.Returns(tenantId);
        tenantContext.UserId.Returns(Guid.Empty);
        tenantContext.CorrelationId.Returns(Guid.NewGuid());
        tenantContext.RolesByBu.Returns(new Dictionary<Guid, string>());
        clock.UtcNow.Returns(now);
        tokenHasher.Hash("p").Returns(tokenHash);
        identityProvisioner.ProvisionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("uid");

        var invitation = UserInvitation.Create(
            "u@x.com",
            InvitationToken.FromHash(tokenHash),
            tenantId,
            [(Guid.NewGuid(), Role.Viewer)],
            now.AddHours(1),
            now.AddMinutes(-5));

        User? saved = null;
        UserInvitation? savedInv = invitation;

        invitationRepo.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<UserInvitation?>(savedInv));
        invitationRepo.SaveAsync(Arg.Do<UserInvitation>(i => savedInv = i), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(saved));
        userRepo.SaveAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new AcceptInvitationCommandHandler(
            invitationRepo, userRepo, identityProvisioner, outbox, tenantContext, clock, tokenHasher);

        for (var i = 0; i < n; i++)
        {
            handler.Handle(new AcceptInvitationCommand("p", "Nome"), CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        return (savedInv!.State == InvitationState.Accepted)
            .ToProperty()
            .Label($"convite deve estar Accepted após {n} reprocessamentos; estado={savedInv.State}");
    }
}
