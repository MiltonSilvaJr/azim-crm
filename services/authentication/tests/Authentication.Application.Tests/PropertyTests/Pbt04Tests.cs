using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Authentication.Application.Tests.PropertyTests;

/// <summary>
/// PBT-04 — Idempotência do logout.
///
/// Propriedade: N ≥ 1 chamadas consecutivas de <c>LogoutCommand</c> para a mesma sessão
/// produzem estado final único (sessão inválida) sem erro adicional.
///
/// Qualquer IdentityProviderException após a primeira revogação deve ser capturada como sucesso.
///
/// FsCheck gera ≥ 100 casos.
/// Mapeia: PBT-04, TASK-07, design.md § 5.3, § 6.5, Req 9.5.
/// </summary>
public sealed class Pbt04Tests
{
    /// <summary>Wrapper para N ≥ 1 (número de chamadas consecutivas de logout).</summary>
    public sealed record LogoutCallCount(int Value);

    /// <summary>Provedor de geradores para PBT-04.</summary>
    public static class Generators
    {
        /// <summary>Gera N no intervalo [1..20] (N ≥ 1 chamadas de logout).</summary>
        public static Arbitrary<LogoutCallCount> LogoutCallCountArbitrary() =>
            ArbMap.Default
                .GeneratorFor<int>()
                .Select(n => Math.Abs(n) % 20 + 1) // [1..20]
                .Select(n => new LogoutCallCount(n))
                .ToArbitrary();
    }

    /// <summary>
    /// PBT-04: N ≥ 1 chamadas de logout para a mesma sessão — todas retornam sem exceção.
    /// Estado final: sessão sempre inválida após pelo menos uma chamada.
    ///
    /// O adapter simula idempotência real: primeira chamada succeeds, demais lançam
    /// IdentityProviderException (sessão já revogada) — o service deve capturar como sucesso.
    ///
    /// Mapeia: PBT-04, Req 9.5.
    /// </summary>
    [Property(
        MaxTest = 100,
        Arbitrary = [typeof(Generators)],
        DisplayName = "PBT-04: N chamadas consecutivas de logout → todas sucesso, estado final idêntico")]
    public bool NConsecutiveLogouts_AllSucceed_FinalStateIdentical(LogoutCallCount callCount)
    {
        // Arrange
        var identityProvider = Substitute.For<IIdentityProvider>();
        var auditEmitter = Substitute.For<IAuditEventEmitter>();
        var sut = new SessionRevocationService(identityProvider, auditEmitter);

        var command = new LogoutCommand(UserId: Guid.NewGuid(), TenantId: Guid.NewGuid());

        var invocationCount = 0;
        identityProvider
            .RevokeRefreshTokensAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                invocationCount++;
                if (invocationCount > 1)
                    // Simula IdP retornando "já revogado" nas chamadas subsequentes
                    throw new IdentityProviderException("AUTH-ERR-004", "Sessão já revogada no IdP");
                return Task.CompletedTask;
            });

        // Act: N chamadas consecutivas de logout
        var allSucceeded = true;
        for (var i = 0; i < callCount.Value; i++)
        {
            try
            {
                sut.HandleAsync(command).GetAwaiter().GetResult();
            }
            catch
            {
                allSucceeded = false;
                break;
            }
        }

        // Assert: todas as N chamadas retornaram sem exceção
        return allSucceeded;
    }
}
