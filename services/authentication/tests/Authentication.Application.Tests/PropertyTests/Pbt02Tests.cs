using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Application.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Caching.Memory;

namespace Authentication.Application.Tests.PropertyTests;

/// <summary>
/// PBT-02 — Isolamento cross-tenant (unitário na camada Application).
///
/// Propriedade: token com firebase_tenant de tenant A nunca é aceito em contexto de tenant B.
/// Para qualquer par de tenants distintos (A ≠ B), a validação no contexto B
/// deve lançar <see cref="IdentityProviderException"/> AUTH-ERR-004.
///
/// Gerador: pares de inteiros distintos mapeados para strings de firebase_tenant.
/// Mínimo 100 casos gerados pelo FsCheck.
///
/// Mapeia: PBT-02, TASK-06, design.md § 4.6, Req 4.3.
/// </summary>
public sealed class Pbt02Tests
{
    // =========================================================================
    // Gerador de pares de tenant distintos
    // =========================================================================

    /// <summary>Par de firebase_tenant strings distintas para PBT-02.</summary>
    public sealed record CrossTenantPair(string FirebaseTenantA, string FirebaseTenantB);

    /// <summary>Provedor de geradores arbitrários registrado via PropertyAttribute.</summary>
    public static class Generators
    {
        /// <summary>
        /// Gera pares (A, B) onde A ≠ B — strings de firebase_tenant não-vazias.
        /// </summary>
        public static Arbitrary<CrossTenantPair> CrossTenantPairArbitrary()
        {
            var gen = ArbMap.Default
                .GeneratorFor<int>()
                .Select(Math.Abs)
                .Where(n => n > 0)
                .SelectMany(a =>
                    ArbMap.Default
                        .GeneratorFor<int>()
                        .Select(Math.Abs)
                        .Where(b => b > 0 && b != a)
                        .Select(b => new CrossTenantPair(
                            $"firebase-tenant-{a:D6}",
                            $"firebase-tenant-{b:D6}")));

            return gen.ToArbitrary();
        }
    }

    // =========================================================================
    // PBT-02: isolamento cross-tenant
    // =========================================================================

    /// <summary>
    /// PBT-02: token com firebase_tenant de tenant A (A ≠ B) é sempre rejeitado
    /// quando o contexto esperado é tenant B.
    ///
    /// <see cref="SessionTokenValidator"/> aplica <c>TenantMatchSpec</c> e lança
    /// AUTH-ERR-004 quando o firebase_tenant do token não corresponde ao esperado.
    ///
    /// Mapeia: PBT-02, design.md § 4.6, Req 4.3.
    /// </summary>
    [Property(
        MaxTest = 100,
        Arbitrary = [typeof(Generators)],
        DisplayName = "PBT-02: token de tenant A sempre rejeitado em contexto de tenant B")]
    public bool TokenFromTenantA_AlwaysRejectedInContextB(CrossTenantPair tenantPair)
    {
        // Arrange: IIdentityProvider retorna token com firebase_tenant = tenantA
        // mas o contexto da requisição espera tenantB
        var identityProvider = Substitute.For<IIdentityProvider>();
        var tenantIdB = Guid.NewGuid();

        identityProvider
            .VerifyTokenAsync(
                Arg.Any<string>(),
                tenantPair.FirebaseTenantB,
                Arg.Any<CancellationToken>())
            .Returns(new VerifyTokenResult
            {
                ProviderUserRef = "uid-from-a",
                FirebaseTenant = tenantPair.FirebaseTenantA, // token é de A!
                Email = "user@tenant-a.com",
                SignInProvider = "password",
            });

        var validator = new SessionTokenValidator(identityProvider);

        // Act: tenta validar token de A em contexto de B
        var threw = false;
        var correctErrorCode = false;

        try
        {
            validator.ValidateAsync("raw.jwt.a", tenantPair.FirebaseTenantB, tenantIdB)
                .GetAwaiter()
                .GetResult();
        }
        catch (IdentityProviderException ex)
        {
            threw = true;
            correctErrorCode = ex.ErrorCode == "AUTH-ERR-004";
        }
        catch
        {
            threw = true;
            correctErrorCode = false;
        }

        // Assert: deve lançar com código de cross-tenant (PBT-02)
        return threw && correctErrorCode;
    }
}
