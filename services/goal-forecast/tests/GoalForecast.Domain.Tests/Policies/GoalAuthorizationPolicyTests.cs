using FluentAssertions;
using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.Policies;
using GoalForecast.Domain.ValueObjects;
using Xunit;

namespace GoalForecast.Domain.Tests.Policies;

/// <summary>
/// Testes de <see cref="GoalAuthorizationPolicy"/> cobrindo a matriz completa de papéis × escopo.
/// Cobre TASK-07: CanWrite, CanRead, anti-enumeração (GF-ERR-006), INV-4.
/// Mapeia: Req 12, Req 1.4, RNF 2, design §4.6, §10, TASK-07.
/// </summary>
public sealed class GoalAuthorizationPolicyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();
    private static readonly Guid OtherBuId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    // =========================================================================
    // Helpers para criar principals
    // =========================================================================

    private static GoalPrincipal TenantAdmin() =>
        new(TenantId, UserId, GoalRole.TenantAdmin, BuId: null);

    private static GoalPrincipal GestorDaBu() =>
        new(TenantId, UserId, GoalRole.GestorDeBu, BuId: BuId);

    private static GoalPrincipal GestorDeOutraBu() =>
        new(TenantId, UserId, GoalRole.GestorDeBu, BuId: OtherBuId);

    private static GoalPrincipal Executivo() =>
        new(TenantId, UserId, GoalRole.Executivo, BuId: null);

    private static GoalPrincipal Vendedor() =>
        new(TenantId, UserId, GoalRole.Vendedor, BuId: BuId);

    // =========================================================================
    // CanWrite — Tenant Admin (pode criar/editar em qualquer BU)
    // =========================================================================

    [Fact(DisplayName = "TenantAdmin pode escrever meta em qualquer BU")]
    public void TenantAdmin_CanWrite_AnyBu()
    {
        var scope = GoalScope.ForBu(BuId);
        var result = GoalAuthorizationPolicy.CanWrite(TenantAdmin(), scope, TenantId);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact(DisplayName = "TenantAdmin pode escrever meta em BU diferente da 'padrão'")]
    public void TenantAdmin_CanWrite_OtherBu()
    {
        var scope = GoalScope.ForBu(OtherBuId);
        var result = GoalAuthorizationPolicy.CanWrite(TenantAdmin(), scope, TenantId);

        result.IsAllowed.Should().BeTrue();
    }

    // =========================================================================
    // CanWrite — Gestor de BU (pode criar/editar apenas na sua BU)
    // =========================================================================

    [Fact(DisplayName = "GestorDeBu pode escrever meta na sua BU")]
    public void GestorDeBu_CanWrite_OwnBu()
    {
        var scope = GoalScope.ForBu(BuId);
        var result = GoalAuthorizationPolicy.CanWrite(GestorDaBu(), scope, TenantId);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact(DisplayName = "GestorDeBu não pode escrever meta em outra BU")]
    public void GestorDeBu_CannotWrite_OtherBu()
    {
        var scope = GoalScope.ForBu(OtherBuId);
        var result = GoalAuthorizationPolicy.CanWrite(GestorDaBu(), scope, TenantId);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("GF-ERR-006");
    }

    // =========================================================================
    // CanWrite — Executivo e Vendedor (negado)
    // =========================================================================

    [Fact(DisplayName = "Executivo não pode escrever meta (negado)")]
    public void Executivo_CannotWrite()
    {
        var scope = GoalScope.ForBu(BuId);
        var result = GoalAuthorizationPolicy.CanWrite(Executivo(), scope, TenantId);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("GF-ERR-006");
    }

    [Fact(DisplayName = "Vendedor não pode escrever meta (negado)")]
    public void Vendedor_CannotWrite()
    {
        var scope = GoalScope.ForBu(BuId);
        var result = GoalAuthorizationPolicy.CanWrite(Vendedor(), scope, TenantId);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("GF-ERR-006");
    }

    // =========================================================================
    // CanRead — TenantAdmin e Executivo (veem o tenant inteiro)
    // =========================================================================

    [Fact(DisplayName = "TenantAdmin pode ler meta de qualquer BU do tenant")]
    public void TenantAdmin_CanRead_AnyBu()
    {
        var scope = GoalScope.ForBu(OtherBuId);
        var result = GoalAuthorizationPolicy.CanRead(TenantAdmin(), scope, TenantId);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact(DisplayName = "Executivo pode ler meta de qualquer BU do tenant")]
    public void Executivo_CanRead_AnyBu()
    {
        var scope = GoalScope.ForBu(OtherBuId);
        var result = GoalAuthorizationPolicy.CanRead(Executivo(), scope, TenantId);

        result.IsAllowed.Should().BeTrue();
    }

    // =========================================================================
    // CanRead — GestorDeBu (vê só sua BU)
    // =========================================================================

    [Fact(DisplayName = "GestorDeBu pode ler meta da sua BU")]
    public void GestorDeBu_CanRead_OwnBu()
    {
        var scope = GoalScope.ForBu(BuId);
        var result = GoalAuthorizationPolicy.CanRead(GestorDaBu(), scope, TenantId);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact(DisplayName = "GestorDeBu não pode ler meta de BU à qual não pertence")]
    public void GestorDeBu_CannotRead_OtherBu()
    {
        // GestorDeOutraBu tem BuId = OtherBuId; tenta ler meta de BuId (diferente).
        var scope = GoalScope.ForBu(BuId);
        var result = GoalAuthorizationPolicy.CanRead(GestorDeOutraBu(), scope, TenantId);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("GF-ERR-006");
    }

    // =========================================================================
    // CanRead — Vendedor (vê só o próprio owner_id)
    // =========================================================================

    [Fact(DisplayName = "Vendedor pode ler meta onde é o próprio owner")]
    public void Vendedor_CanRead_OwnMeta()
    {
        var scope = GoalScope.ForResponsavel(BuId, UserId); // UserId == Vendedor.UserId
        var result = GoalAuthorizationPolicy.CanRead(Vendedor(), scope, TenantId);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact(DisplayName = "Vendedor não pode ler meta de outro owner")]
    public void Vendedor_CannotRead_OtherOwnerMeta()
    {
        var scope = GoalScope.ForResponsavel(BuId, OtherUserId);
        var result = GoalAuthorizationPolicy.CanRead(Vendedor(), scope, TenantId);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("GF-ERR-006");
    }

    // =========================================================================
    // Anti-enumeração: negação não revela existência (RNF-2.3)
    // =========================================================================

    [Fact(DisplayName = "Negação de autorização retorna GF-ERR-006 sem revelar detalhes")]
    public void Denial_ReturnsGfErr006_WithoutRevealingDetails()
    {
        var scope = GoalScope.ForBu(OtherBuId);
        var writeResult = GoalAuthorizationPolicy.CanWrite(Vendedor(), scope, TenantId);
        var readResult = GoalAuthorizationPolicy.CanRead(Vendedor(), scope, TenantId);

        writeResult.ErrorCode.Should().Be("GF-ERR-006");
        readResult.ErrorCode.Should().Be("GF-ERR-006");
    }

    // =========================================================================
    // INV-4: tenant_id divergente é negado
    // =========================================================================

    [Fact(DisplayName = "TenantId divergente entre principal e tenant alvo é negado")]
    public void DivergentTenantId_IsDenied()
    {
        var scope = GoalScope.ForBu(BuId);
        var result = GoalAuthorizationPolicy.CanWrite(TenantAdmin(), scope, OtherTenantId);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("GF-ERR-006");
    }
}
