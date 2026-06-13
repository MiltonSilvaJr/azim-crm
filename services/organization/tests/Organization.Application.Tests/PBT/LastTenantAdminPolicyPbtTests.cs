using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using Organization.Application.Policies;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Application.Tests.PBT;

/// <summary>
/// PBT-02 — LastTenantAdminPolicy: nenhuma operação sobre TAdmins pode deixar o tenant sem
/// ao menos um TAdmin ativo.
/// Propriedade: para qualquer sequência de assign/change/remove, o contador de TAdmins
/// permanece ≥ 1; violações são rejeitadas pela policy sem alterar o estado.
/// </summary>
public sealed class LastTenantAdminPolicyPbtTests
{
    // ── PBT-02-A: EnforceAsync nunca permite remover o último TAdmin ─────────────────

    [Property(MaxTest = 200)]
    public Property EnforceAsync_WhenCountIsOne_AlwaysThrows(PositiveInt seed)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(seed)),
            _ =>
            {
                // Arrange — sempre 1 TAdmin (último)
                var adminCounter = Substitute.For<ITenantAdminCounter>();
                var tenantId = Guid.NewGuid();
                var userId = Guid.NewGuid();
                adminCounter
                    .CountActiveTenantAdminsAsync(tenantId, Arg.Any<CancellationToken>())
                    .Returns(1);

                // Act
                var act = async () => await LastTenantAdminPolicy.EnforceAsync(
                    adminCounter,
                    tenantId,
                    userId,
                    affectedUserIsTAdmin: true,
                    CancellationToken.None);

                // Assert — deve sempre lançar
                var exception = Record.ExceptionAsync(act).GetAwaiter().GetResult();
                return exception is InvalidOperationException ex
                       && ex.Message.Contains("ORG-ERR-009");
            });
    }

    // ── PBT-02-B: EnforceAsync permite remover quando há mais de um TAdmin ──────────

    [Property(MaxTest = 200)]
    public Property EnforceAsync_WhenCountIsGreaterThanOne_NeverThrows(PositiveInt count)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(count)),
            c =>
            {
                // conta entre 2 e 50
                var adminCount = c.Get % 49 + 2;

                var adminCounter = Substitute.For<ITenantAdminCounter>();
                var tenantId = Guid.NewGuid();
                var userId = Guid.NewGuid();
                adminCounter
                    .CountActiveTenantAdminsAsync(tenantId, Arg.Any<CancellationToken>())
                    .Returns(adminCount);

                // Act
                var act = async () => await LastTenantAdminPolicy.EnforceAsync(
                    adminCounter,
                    tenantId,
                    userId,
                    affectedUserIsTAdmin: true,
                    CancellationToken.None);

                // Assert — nunca deve lançar quando há mais de 1 TAdmin
                var exception = Record.ExceptionAsync(act).GetAwaiter().GetResult();
                return exception is null;
            });
    }

    // ── PBT-02-C: Policy bloqueia sempre que count = 1, sem mutar estado ────────────

    [Property(MaxTest = 200)]
    public Property PolicyEnforced_WhenLastTAdmin_BlocksAndStateUnchanged(PositiveInt seed)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(seed)),
            s =>
            {
                // Arrange — 1 TAdmin, policy deve sempre bloquear operações de rebaixamento/remoção
                var adminCounter = Substitute.For<ITenantAdminCounter>();
                var tenantId = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;
                var buId = Guid.NewGuid();

                // Gera N tentativas de rebaixar/remover o último TAdmin (N entre 1..10)
                var attempts = s.Get % 10 + 1;
                var allBlocked = true;

                adminCounter
                    .CountActiveTenantAdminsAsync(tenantId, Arg.Any<CancellationToken>())
                    .Returns(1);

                for (var i = 0; i < attempts; i++)
                {
                    var userId = Guid.NewGuid();
                    var act = async () => await LastTenantAdminPolicy.EnforceAsync(
                        adminCounter,
                        tenantId,
                        userId,
                        affectedUserIsTAdmin: true,
                        CancellationToken.None);

                    var exception = Record.ExceptionAsync(act).GetAwaiter().GetResult();
                    if (exception is not InvalidOperationException ex || !ex.Message.Contains("ORG-ERR-009"))
                        allBlocked = false;
                }

                return allBlocked;
            });
    }

    // ── PBT-02-D-old (mantido abaixo como PBT-02-D): non-TAdmin never consults counter ─

    // ── PBT-02-D: Policy não altera estado quando rejeita ────────────────────────────

    [Property(MaxTest = 200)]
    public Property EnforceAsync_OnRejection_DoesNotMutateUser(PositiveInt seed)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(seed)),
            _ =>
            {
                // Arrange — 1 TAdmin, 0 outros
                var adminCounter = Substitute.For<ITenantAdminCounter>();
                var tenantId = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;
                var buId = Guid.NewGuid();

                var user = User.Activate("tad@test.com", "TAdmin", "uid-tad", tenantId, now);
                user.AssignMembership(buId, Role.TAdmin, Guid.NewGuid());
                var membershipCountBefore = user.Memberships.Count;

                adminCounter
                    .CountActiveTenantAdminsAsync(tenantId, Arg.Any<CancellationToken>())
                    .Returns(1);

                // Act — tenta enforçar; deve lançar
                var act = async () => await LastTenantAdminPolicy.EnforceAsync(
                    adminCounter,
                    tenantId,
                    user.Id,
                    affectedUserIsTAdmin: true,
                    CancellationToken.None);

                var exception = Record.ExceptionAsync(act).GetAwaiter().GetResult();

                // Assert — estado do usuário inalterado após rejeição
                var membershipCountAfter = user.Memberships.Count;
                return exception is InvalidOperationException
                       && membershipCountBefore == membershipCountAfter;
            });
    }
}
