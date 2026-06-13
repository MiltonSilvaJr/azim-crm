using Authentication.Domain.ValueObjects;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Authentication.Domain.Tests.PropertyTests;

/// <summary>
/// PBT-01 — Sessão sempre escopada a um único tenant_id.
///
/// Para qualquer <see cref="AuthContext"/> gerado, <c>tenant_id</c> é único,
/// não nulo e exatamente igual ao tenant resolvido (design.md § 4.3, PBT-01).
///
/// FsCheck gera ≥ 100 casos (MaxTest = 100).
/// Mapeia: PBT-01, Req 5.2, Req 5.3, design.md § 4.3.
/// </summary>
public sealed class Pbt01Tests
{
    // =========================================================================
    // Arbitrary customizado para NonEmptyGuid
    // =========================================================================

    /// <summary>
    /// Invólucro de Guid não-vazio usado como tipo de entrada nos testes de propriedade.
    /// Permite ao FsCheck gerar valores via o tipo wrapper sem precisar de Arb estático.
    /// </summary>
    public sealed record NonEmptyGuid(Guid Value);

    /// <summary>
    /// Provedor de geradores arbitrários registrado via <see cref="PropertyAttribute.Arbitrary"/>.
    /// </summary>
    public static class Generators
    {
        /// <summary>Gerador de <see cref="NonEmptyGuid"/> (Guid nunca vazio).</summary>
        public static Arbitrary<NonEmptyGuid> NonEmptyGuidArbitrary() =>
            ArbMap.Default
                .GeneratorFor<Guid>()
                .Where(g => g != Guid.Empty)
                .Select(g => new NonEmptyGuid(g))
                .ToArbitrary();
    }

    // =========================================================================
    // PBT-01 — tenant_id preservado fielmente no AuthContext
    // =========================================================================

    /// <summary>
    /// PBT-01: para qualquer tenant_id válido (não-vazio), o <see cref="AuthContext"/>
    /// o preserva fielmente (não altera, não zera, não substitui).
    ///
    /// Verifica que a sessão está sempre escopada ao tenant correto.
    ///
    /// Mapeia: PBT-01, design.md § 4.3, Req 5.2, Req 5.3.
    /// </summary>
    [Property(
        MaxTest = 100,
        Arbitrary = [typeof(Generators)],
        DisplayName = "PBT-01: tenant_id é preservado fielmente no AuthContext")]
    public bool Pbt01_TenantId_IsPreservedFromInput(NonEmptyGuid tenantIdWrapper)
    {
        var tenantId = tenantIdWrapper.Value;

        var ctx = AuthContext.Create(
            Guid.NewGuid(),
            tenantId,
            "user@x.com",
            [],
            MembershipSet.Create([], DateTimeOffset.UtcNow));

        // (a) tenant_id preservado fielmente
        // (b) tenant_id nunca vazio (PBT-01: não nulo)
        return ctx.TenantId == tenantId && ctx.TenantId != Guid.Empty;
    }

    /// <summary>
    /// PBT-01 (complementar): para qualquer par (userId, tenantId) válidos,
    /// o AuthContext tem exatamente um tenant_id (campo escalar — não coleção).
    ///
    /// "Exatamente um" é garantido estruturalmente pelo tipo Guid (escalar),
    /// verificamos que o valor gerado é preservado e distinto de Guid.Empty.
    ///
    /// Mapeia: PBT-01, design.md § 4.3.
    /// </summary>
    [Property(
        MaxTest = 100,
        Arbitrary = [typeof(Generators)],
        DisplayName = "PBT-01 complementar: AuthContext sempre tem exatamente um tenant_id")]
    public bool Pbt01_AuthContext_HasExactlyOneTenantId(NonEmptyGuid userIdWrapper, NonEmptyGuid tenantIdWrapper)
    {
        var ctx = AuthContext.Create(
            userIdWrapper.Value,
            tenantIdWrapper.Value,
            "user@tenant.io",
            [],
            MembershipSet.Create([], DateTimeOffset.UtcNow));

        return ctx.TenantId == tenantIdWrapper.Value && ctx.TenantId != Guid.Empty;
    }
}
