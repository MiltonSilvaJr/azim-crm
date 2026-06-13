using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Services;
using Authentication.Domain.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Authentication.Application.Tests.PropertyTests;

/// <summary>
/// PBT-05 — Links em estado expirado/consumido nunca ativam conta (unitário).
///
/// Propriedade: nenhuma sequência de operações reabilita um link em estado terminal
/// (<c>expired</c> ou <c>consumed</c>).
///
/// FsCheck gera ≥ 100 casos com links em estados terminais.
/// Mapeia: PBT-05, TASK-08, design.md § 16.5, Req 7.4, Req 7.5.
/// </summary>
public sealed class Pbt05Tests
{
    /// <summary>Wrapper para estado terminal de link de convite.</summary>
    public sealed record TerminalLinkState(InviteLinkState State, DateTimeOffset ExpiresAt);

    /// <summary>Provedor de geradores para PBT-05.</summary>
    public static class Generators
    {
        /// <summary>
        /// Gera links em estados terminais (Expired ou Consumed).
        /// Inclui variações de ExpiresAt (passado e futuro) para cobrir
        /// o caso de link Issued mas com prazo atingido.
        /// </summary>
        public static Arbitrary<TerminalLinkState> TerminalLinkStateArbitrary()
        {
            var expiredState = Gen.Constant(InviteLinkState.Expired);
            var consumedState = Gen.Constant(InviteLinkState.Consumed);
            var issuedExpiredState = Gen.Constant(InviteLinkState.Issued);

            // Gera offset de minutos passados [-1440..-1] para simular prazo atingido
            var pastOffset = ArbMap.Default
                .GeneratorFor<int>()
                .Select(n => Math.Abs(n) % 1440 + 1)
                .Select(minutes => DateTimeOffset.UtcNow.AddMinutes(-minutes));

            // Estado Expired com ExpiresAt no futuro (já foi marcado como expired)
            var expiredGen = Gen.Zip(expiredState, pastOffset)
                .Select(t => new TerminalLinkState(t.Item1, t.Item2));

            // Estado Consumed com ExpiresAt no futuro (já foi consumido)
            var futureOffset = ArbMap.Default
                .GeneratorFor<int>()
                .Select(n => Math.Abs(n) % 1440 + 1)
                .Select(minutes => DateTimeOffset.UtcNow.AddMinutes(minutes));

            var consumedGen = Gen.Zip(consumedState, futureOffset)
                .Select(t => new TerminalLinkState(t.Item1, t.Item2));

            // Estado Issued mas com ExpiresAt no passado (expirado por tempo)
            var issuedExpiredGen = Gen.Zip(issuedExpiredState, pastOffset)
                .Select(t => new TerminalLinkState(t.Item1, t.Item2));

            return Gen.OneOf(expiredGen, consumedGen, issuedExpiredGen)
                .ToArbitrary();
        }
    }

    /// <summary>
    /// PBT-05: link em estado terminal (expired, consumed ou issued+prazo) nunca ativa conta.
    /// InviteActivationService.ActivateAsync deve sempre lançar AUTH-ERR-033.
    ///
    /// Mapeia: PBT-05, design.md § 16.5, Req 7.4, Req 7.5.
    /// </summary>
    [Property(
        MaxTest = 100,
        Arbitrary = [typeof(Generators)],
        DisplayName = "PBT-05: link expirado/consumido nunca ativa conta (AUTH-ERR-033)")]
    public bool TerminalLinkState_NeverActivatesAccount(TerminalLinkState terminalLink)
    {
        // Arrange
        var sut = new InviteActivationService(
            Substitute.For<IIdentityProvider>(),
            Substitute.For<IUserDirectory>(),
            Substitute.For<IEmailSender>(),
            Substitute.For<IAuditEventEmitter>());

        var command = new ActivateInviteCommand(
            ActivationToken: "any-token",
            State: terminalLink.State,
            ExpiresAt: terminalLink.ExpiresAt,
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        // Act
        var threw = false;
        var correctErrorCode = false;

        try
        {
            sut.ActivateAsync(command).GetAwaiter().GetResult();
        }
        catch (IdentityProviderException ex)
        {
            threw = true;
            correctErrorCode = ex.ErrorCode == "AUTH-ERR-033";
        }
        catch
        {
            threw = true;
            correctErrorCode = false;
        }

        // Assert: link terminal sempre rejeitado com AUTH-ERR-033
        return threw && correctErrorCode;
    }
}
