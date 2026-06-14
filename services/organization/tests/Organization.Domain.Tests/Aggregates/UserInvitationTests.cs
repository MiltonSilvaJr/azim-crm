using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Organization.Domain.Aggregates;
using Organization.Domain.Events;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Domain.Tests.Aggregates;

/// <summary>
/// Testes unitários e PBT-04 para o agregado <see cref="UserInvitation"/>.
/// Cobre: criação, state machine (pending → accepted/revoked/expired),
/// transições inválidas a partir de estados terminais, expiração no aceite.
/// PBT-04: sequências aleatórias de transições; estados terminais rejeitam toda transição.
/// </summary>
public sealed class UserInvitationTests
{
    private static readonly Guid TenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid BuId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset Now = new(2026, 6, 13, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expiry = Now.AddHours(72);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WhenValid_ShouldBePending()
    {
        var inv = CreateInvitation();
        inv.State.Should().Be(InvitationState.Pending);
    }

    [Fact]
    public void Create_ShouldRaiseUserInvitedEvent()
    {
        var inv = CreateInvitation();
        inv.DomainEvents.Should().ContainSingle(e => e is UserInvited);
    }

    [Fact]
    public void Create_WhenEmailIsNull_ShouldThrow()
    {
        var act = () => UserInvitation.Create(
            null!, InvitationToken.FromHash("hash"), TenantId,
            [(BuId, Role.TAdmin)], Expiry, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenTargetMembershipsIsEmpty_ShouldThrow()
    {
        var act = () => UserInvitation.Create(
            "user@example.com", InvitationToken.FromHash("hash"), TenantId,
            [], Expiry, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_ShouldSetTenantId()
    {
        var inv = CreateInvitation();
        inv.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public void Create_ShouldSetExpiresAt()
    {
        var inv = CreateInvitation();
        inv.ExpiresAt.Should().Be(Expiry);
    }

    // ── Accept: transição válida ───────────────────────────────────────────────

    [Fact]
    public void Accept_WhenPendingAndTokenValidAndNotExpired_ShouldBeAccepted()
    {
        var inv = CreateInvitation("myhash");
        inv.Accept("myhash", Now.AddHours(1));
        inv.State.Should().Be(InvitationState.Accepted);
    }

    [Fact]
    public void Accept_WhenExpired_ShouldThrow()
    {
        var inv = CreateInvitation("myhash");
        var afterExpiry = Expiry.AddSeconds(1);
        var act = () => inv.Accept("myhash", afterExpiry);
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-004*");
    }

    [Fact]
    public void Accept_WhenWrongToken_ShouldThrow()
    {
        var inv = CreateInvitation("correcthash");
        var act = () => inv.Accept("wronghash", Now.AddHours(1));
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-004*");
    }

    [Fact]
    public void Accept_WhenExactlyAtExpiry_ShouldThrow()
    {
        var inv = CreateInvitation("myhash");
        // expiresAt é exclusivo: now >= expiresAt → expirado
        var act = () => inv.Accept("myhash", Expiry);
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-004*");
    }

    // ── Revoke: transição válida ──────────────────────────────────────────────

    [Fact]
    public void Revoke_WhenPending_ShouldBeRevoked()
    {
        var inv = CreateInvitation();
        inv.Revoke(Now);
        inv.State.Should().Be(InvitationState.Revoked);
    }

    // ── Expire: transição válida ──────────────────────────────────────────────

    [Fact]
    public void Expire_WhenPending_ShouldBeExpired()
    {
        var inv = CreateInvitation();
        inv.Expire(Now);
        inv.State.Should().Be(InvitationState.Expired);
    }

    // ── Transições inválidas a partir de estados terminais ────────────────────

    [Fact]
    public void Accept_WhenAlreadyAccepted_ShouldThrow()
    {
        var inv = CreateInvitation("myhash");
        inv.Accept("myhash", Now.AddHours(1));
        var act = () => inv.Accept("myhash", Now.AddHours(2));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Accept_WhenRevoked_ShouldThrow()
    {
        var inv = CreateInvitation("myhash");
        inv.Revoke(Now);
        var act = () => inv.Accept("myhash", Now.AddHours(1));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Accept_WhenExpired_AfterExpireCall_ShouldThrow()
    {
        var inv = CreateInvitation("myhash");
        inv.Expire(Now);
        var act = () => inv.Accept("myhash", Now.AddHours(1));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Revoke_WhenAccepted_ShouldThrow()
    {
        var inv = CreateInvitation("myhash");
        inv.Accept("myhash", Now.AddHours(1));
        var act = () => inv.Revoke(Now.AddHours(2));
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-006*");
    }

    [Fact]
    public void Revoke_WhenRevoked_ShouldThrow()
    {
        var inv = CreateInvitation();
        inv.Revoke(Now);
        var act = () => inv.Revoke(Now.AddHours(1));
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-006*");
    }

    [Fact]
    public void Revoke_WhenExpired_ShouldThrow()
    {
        var inv = CreateInvitation();
        inv.Expire(Now);
        var act = () => inv.Revoke(Now.AddHours(1));
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-006*");
    }

    [Fact]
    public void Expire_WhenAccepted_ShouldThrow()
    {
        var inv = CreateInvitation("myhash");
        inv.Accept("myhash", Now.AddHours(1));
        var act = () => inv.Expire(Now.AddHours(2));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Expire_WhenRevoked_ShouldThrow()
    {
        var inv = CreateInvitation();
        inv.Revoke(Now);
        var act = () => inv.Expire(Now.AddHours(1));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Expire_WhenExpired_ShouldThrow()
    {
        var inv = CreateInvitation();
        inv.Expire(Now);
        var act = () => inv.Expire(Now.AddHours(1));
        act.Should().Throw<DomainException>();
    }

    // ── PBT-04: State machine — sequências aleatórias ─────────────────────────

    /// <summary>
    /// PBT-04: gera sequências aleatórias de transições (Accept, Revoke, Expire).
    /// Afirma:
    /// 1. Apenas transições a partir de Pending são válidas.
    /// 2. Estados terminais (Accepted/Revoked/Expired) rejeitam toda transição.
    /// 3. Estado final é consistente (nunca inválido).
    /// Mínimo de 100 exemplos.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Pbt04_StateMachineTransitions(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var hash = $"hash_{seed.Get}";
        var inv = UserInvitation.Create(
            $"user{seed.Get}@example.com",
            InvitationToken.FromHash(hash),
            TenantId,
            [(BuId, Role.TAdmin)],
            Expiry,
            Now);

        var validTransitionDone = false;
        var terminalStateReachedBeforeRejection = true;

        for (var i = 0; i < 5; i++)
        {
            var wasTerminal = inv.State != InvitationState.Pending;
            var op = rng.Next(3);

            var threw = false;
            try
            {
                switch (op)
                {
                    case 0: inv.Accept(hash, Now.AddHours(1)); break;
                    case 1: inv.Revoke(Now.AddHours(i)); break;
                    case 2: inv.Expire(Now.AddHours(i)); break;
                }

                if (wasTerminal)
                {
                    // Transitou a partir de estado terminal sem jogar — invariante violada
                    terminalStateReachedBeforeRejection = false;
                }
                else
                {
                    validTransitionDone = true;
                }
            }
            catch (DomainException)
            {
                threw = true;
                if (!wasTerminal && !validTransitionDone)
                {
                    // Transição a partir de Pending foi rejeitada — válido apenas se token/expiração errada
                }
            }

            // Se estava em estado terminal e não jogou, invariante violada
            if (wasTerminal && !threw)
                terminalStateReachedBeforeRejection = false;
        }

        // O estado final deve ser sempre um valor válido do enum
        var finalStateIsValid = inv.State is
            InvitationState.Pending or
            InvitationState.Accepted or
            InvitationState.Revoked or
            InvitationState.Expired;

        return (finalStateIsValid && terminalStateReachedBeforeRejection)
            .ToProperty()
            .Label($"finalState={inv.State} terminalProtected={terminalStateReachedBeforeRejection}");
    }

    /// <summary>
    /// PBT-04 (complementar): estado terminal nunca muda após ser atingido.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Pbt04_TerminalStateIsAbsorbing(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var hash = "fixedhash";
        var inv = UserInvitation.Create(
            "test@example.com", InvitationToken.FromHash(hash), TenantId,
            [(BuId, Role.TAdmin)], Expiry, Now);

        // Leva o convite a um estado terminal
        var terminal = rng.Next(3);
        switch (terminal)
        {
            case 0: inv.Accept(hash, Now.AddHours(1)); break;
            case 1: inv.Revoke(Now); break;
            case 2: inv.Expire(Now); break;
        }

        var stateAfterFirstTransition = inv.State;

        // Tenta qualquer outra transição — todas devem jogar
        for (var i = 0; i < 3; i++)
        {
            try
            {
                switch (i)
                {
                    case 0: inv.Accept(hash, Now.AddHours(i + 2)); break;
                    case 1: inv.Revoke(Now.AddHours(i + 2)); break;
                    case 2: inv.Expire(Now.AddHours(i + 2)); break;
                }
            }
            catch (DomainException) { /* esperado */ }
        }

        // Estado não deve ter mudado
        return (inv.State == stateAfterFirstTransition)
            .ToProperty()
            .Label($"terminal={terminal} state={inv.State}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserInvitation CreateInvitation(string hash = "defaulthash")
        => UserInvitation.Create(
            "convidado@example.com",
            InvitationToken.FromHash(hash),
            TenantId,
            [(BuId, Role.TAdmin)],
            Expiry,
            Now);
}
