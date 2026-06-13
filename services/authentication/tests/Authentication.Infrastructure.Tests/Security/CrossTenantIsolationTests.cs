using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using Authentication.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Authentication.Infrastructure.Tests.Security;

/// <summary>
/// PBT-02 — Isolamento cross-tenant: token válido de tenant A nunca produz AuthContext em B.
///
/// Testa a invariante de que um token JWT emitido para o tenant A é rejeitado
/// quando a requisição está em contexto do tenant B (HTTP 401), e que nenhum
/// <see cref="AuthContext"/> é produzido para o tenant errado.
///
/// Mapeia: TASK-25, PBT-02, RNF 1.4, design.md § 13, § 14.
/// </summary>
public sealed class CrossTenantIsolationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string FirebaseTenantA = "firebase-tenant-a";
    private const string FirebaseTenantB = "firebase-tenant-b";

    /// <summary>
    /// PBT-02 — Para qualquer par (tenantA, tenantB) distinto, token emitido para A
    /// deve ser rejeitado quando validado em contexto de B.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TokenOfTenantA_IsRejectedInContextOfTenantB()
    {
        return Prop.ForAll(
            TenantPairGen(),
            pair =>
            {
                // Arrange — IdP rejeita token quando o firebase tenant esperado não corresponde
                var idp = Substitute.For<IIdentityProvider>();
                idp.VerifyTokenAsync(
                        Arg.Any<string>(),
                        Arg.Is<string>(t => t == pair.FirebaseTenantB),
                        Arg.Any<CancellationToken>())
                   .ThrowsAsync(new IdentityProviderException(
                       "AUTH-ERR-004", "Token tenant mismatch"));

                var tokenValidator = new SessionTokenValidator(idp);

                // Act — tentar validar token de A em contexto de B
                var threw = false;
                try
                {
                    tokenValidator.ValidateAsync(
                        rawJwt: "token-from-tenant-a",
                        expectedFirebaseTenant: pair.FirebaseTenantB,
                        tenantId: pair.TenantB,
                        cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (IdentityProviderException ex) when (ex.ErrorCode == "AUTH-ERR-004")
                {
                    threw = true;
                }
                catch
                {
                    threw = true;
                }

                return threw;
            });
    }

    /// <summary>
    /// Determinístico: token do tenant A explicitamente rejeitado no tenant B.
    /// </summary>
    [Fact]
    public async Task TokenFromTenantA_IsRejected_WhenValidatedInTenantBContext()
    {
        // Arrange
        var idp = Substitute.For<IIdentityProvider>();
        idp.VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Is<string>(t => t == FirebaseTenantB),
                Arg.Any<CancellationToken>())
           .ThrowsAsync(new IdentityProviderException("AUTH-ERR-004", "Tenant mismatch"));

        var tokenValidator = new SessionTokenValidator(idp);

        // Act
        Func<Task> act = async () =>
            await tokenValidator.ValidateAsync(
                rawJwt: "jwt-from-tenant-a",
                expectedFirebaseTenant: FirebaseTenantB,
                tenantId: TenantB,
                cancellationToken: CancellationToken.None);

        // Assert — deve lançar IdentityProviderException com AUTH-ERR-004
        await act.Should().ThrowAsync<IdentityProviderException>()
            .Where(ex => ex.ErrorCode == "AUTH-ERR-004");
    }

    /// <summary>
    /// Nenhum AuthContext é produzido para o tenant errado; nenhuma consulta ao diretório de B.
    /// </summary>
    [Fact]
    public async Task AuthContextComposer_WithCrossTenantToken_ProducesNoContextAndQueriesNoDirectory()
    {
        // Arrange
        var idp = Substitute.For<IIdentityProvider>();
        var userDir = Substitute.For<IUserDirectory>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        // IdP rejeita token de A ao verificar para o tenant B
        idp.VerifyTokenAsync(
                Arg.Any<string>(),
                Arg.Is<string>(t => t == FirebaseTenantB),
                Arg.Any<CancellationToken>())
           .ThrowsAsync(new IdentityProviderException("AUTH-ERR-004", "Mismatch"));

        var tokenValidator = new SessionTokenValidator(idp);
        var composer = new AuthContextComposer(userDir, cache);

        // Act — tentar validar token de A em contexto de B
        Func<Task> act = async () =>
        {
            var (_, verifyResult) = await tokenValidator.ValidateAsync(
                rawJwt: "jwt-from-tenant-a",
                expectedFirebaseTenant: FirebaseTenantB,
                tenantId: TenantB,
                cancellationToken: CancellationToken.None);

            // Esta linha nunca deve ser alcançada
            await composer.ComposeAsync(verifyResult, TenantB, CancellationToken.None);
        };

        // Assert — exceção antes de qualquer consulta ao diretório
        await act.Should().ThrowAsync<IdentityProviderException>();
        await userDir.DidNotReceive().FindUserAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // Gerador de pares de tenants distintos
    private static Arbitrary<(Guid TenantA, string FirebaseTenantA, Guid TenantB, string FirebaseTenantB)> TenantPairGen()
    {
        var gen =
            from a in Gen.Fresh(Guid.NewGuid)
            from b in Gen.Fresh(Guid.NewGuid)
            where a != b
            select (a, $"firebase-{a:N}", b, $"firebase-{b:N}");
        return gen.ToArbitrary();
    }
}
