using Authentication.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Authentication.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários de <see cref="AuthContext"/>.
///
/// Mapeia: Req 5.2, 5.3 (composição do AuthContext), design.md § 4.3, PBT-01.
/// Critérios: imutabilidade, igualdade por valor, invariantes obrigatórias,
/// ausência de identity_uid (verificada por inspeção de membros).
/// </summary>
public sealed class AuthContextTests
{
    // =========================================================================
    // Invariantes de criação
    // =========================================================================

    [Fact(DisplayName = "AuthContext deve lançar ArgumentException para user_id vazio")]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        var act = () => AuthContext.Create(
            userId: Guid.Empty,
            tenantId: Guid.NewGuid(),
            email: "user@example.com",
            roles: ["admin"],
            memberships: MembershipSet.Create([], DateTimeOffset.UtcNow));

        act.Should().Throw<ArgumentException>("user_id não pode ser Guid.Empty");
    }

    [Fact(DisplayName = "AuthContext deve lançar ArgumentException para tenant_id vazio")]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => AuthContext.Create(
            userId: Guid.NewGuid(),
            tenantId: Guid.Empty,
            email: "user@example.com",
            roles: ["admin"],
            memberships: MembershipSet.Create([], DateTimeOffset.UtcNow));

        act.Should().Throw<ArgumentException>("tenant_id não pode ser Guid.Empty");
    }

    [Theory(DisplayName = "AuthContext deve lançar ArgumentException para email nulo ou vazio")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ShouldThrow(string? email)
    {
        var act = () => AuthContext.Create(
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            email: email!,
            roles: ["admin"],
            memberships: MembershipSet.Create([], DateTimeOffset.UtcNow));

        act.Should().Throw<ArgumentException>("email é obrigatório");
    }

    [Fact(DisplayName = "AuthContext válido deve ser criado com sucesso")]
    public void Create_WithValidData_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var email = "user@example.com";
        var roles = new[] { "admin" };
        var memberships = MembershipSet.Create([], DateTimeOffset.UtcNow);

        var ctx = AuthContext.Create(userId, tenantId, email, roles, memberships);

        ctx.UserId.Should().Be(userId);
        ctx.TenantId.Should().Be(tenantId);
        ctx.Email.Should().Be(email);
        ctx.Roles.Should().BeEquivalentTo(roles);
    }

    [Fact(DisplayName = "AuthContext não deve conter membro com nome identity_uid ou IdentityUid")]
    public void Create_ShouldNotContainIdentityUidMember()
    {
        // Garante que AuthContext não carrega o identificador externo do IdP (DD-001, Req 5.3)
        var type = typeof(AuthContext);
        var allMembers = type.GetMembers(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Static);

        var violations = allMembers
            .Where(m =>
                m.Name.Contains("identity_uid", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("IdentityUid", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Name)
            .ToList();

        violations.Should().BeEmpty(
            because: "AuthContext nunca deve conter identity_uid — esse símbolo pertence exclusivamente ao adapter (DD-001, Req 5.3)");
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "Dois AuthContext com mesmos dados devem ser iguais")]
    public void Equality_SameData_ShouldBeEqual()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var memberships = MembershipSet.Create([], DateTimeOffset.UtcNow);

        var a = AuthContext.Create(userId, tenantId, "user@x.com", ["admin"], memberships);
        var b = AuthContext.Create(userId, tenantId, "user@x.com", ["admin"], memberships);

        a.Should().Be(b);
    }

    [Fact(DisplayName = "AuthContext com tenant_id diferente não deve ser igual")]
    public void Equality_DifferentTenantId_ShouldNotBeEqual()
    {
        var userId = Guid.NewGuid();
        var memberships = MembershipSet.Create([], DateTimeOffset.UtcNow);

        var a = AuthContext.Create(userId, Guid.NewGuid(), "user@x.com", ["admin"], memberships);
        var b = AuthContext.Create(userId, Guid.NewGuid(), "user@x.com", ["admin"], memberships);

        a.Should().NotBe(b);
    }

    // =========================================================================
    // Imutabilidade — ausência de setters públicos
    // =========================================================================

    [Theory(DisplayName = "AuthContext não deve expor setter público em nenhuma propriedade")]
    [InlineData(nameof(AuthContext.UserId))]
    [InlineData(nameof(AuthContext.TenantId))]
    [InlineData(nameof(AuthContext.Email))]
    [InlineData(nameof(AuthContext.Roles))]
    [InlineData(nameof(AuthContext.Memberships))]
    public void Immutability_Properties_ShouldHaveNoPublicSetter(string propertyName)
    {
        var prop = typeof(AuthContext).GetProperty(propertyName);
        prop.Should().NotBeNull();
        prop!.SetMethod?.IsPublic.Should().BeFalse(
            $"{propertyName} deve ser somente leitura — AuthContext é imutável após composição (design.md § 4.3)");
    }

    // =========================================================================
    // Invariante PBT-01 — exatamente um tenant_id (versão determinística)
    // =========================================================================

    [Fact(DisplayName = "AuthContext sempre carrega exatamente um tenant_id (invariante PBT-01)")]
    public void TenantId_IsAlwaysExactlyOne()
    {
        // Versão determinística do PBT-01; a versão com FsCheck está em Pbt01Tests.cs
        var tenantId = Guid.NewGuid();
        var ctx = AuthContext.Create(
            Guid.NewGuid(), tenantId, "x@x.com", [],
            MembershipSet.Create([], DateTimeOffset.UtcNow));

        ctx.TenantId.Should().Be(tenantId);
        ctx.TenantId.Should().NotBe(Guid.Empty);
    }
}
