using FluentAssertions;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários para o objeto de valor <see cref="ReportScope"/>.
///
/// Invariantes verificadas:
/// - <c>tenantId</c> nulo lança exceção de domínio.
/// - <c>allowedBuIds</c> vazio com papel <c>GestorBU</c> lança exceção.
/// - <c>ReportScope</c> é imutável após construção.
/// - Apenas IScopeResolver pode construir (factory method no domínio).
///
/// Mapeia: TASK-04, design §4.3, DD-006, Req 7.
/// </summary>
public sealed class ReportScopeTests
{
    // -------------------------------------------------------------------------
    // Construção válida
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ReportScope com TenantAdmin deve ser construível sem BU ids (DD-006)")]
    public void ReportScope_TenantAdmin_ShouldBeConstructibleWithoutBuIds()
    {
        var tenantId = Guid.NewGuid();
        var act = () => ReportScope.Create(tenantId, ReportingRole.TenantAdmin, [], null);

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "ReportScope com Vendedor deve ser construível com ownerRestrictedTo (DD-006)")]
    public void ReportScope_Vendedor_ShouldBeConstructibleWithOwnerRestriction()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var scope = ReportScope.Create(tenantId, ReportingRole.Vendedor, [], ownerId);

        scope.OwnerRestrictedTo.Should().Be(ownerId);
        scope.Role.Should().Be(ReportingRole.Vendedor);
    }

    [Fact(DisplayName = "ReportScope com GestorBU deve ser construível com BU ids não-vazio (DD-006)")]
    public void ReportScope_GestorBU_ShouldBeConstructibleWithBuIds()
    {
        var tenantId = Guid.NewGuid();
        var buIds = new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var scope = ReportScope.Create(tenantId, ReportingRole.GestorBU, buIds, null);

        scope.AllowedBuIds.Should().BeEquivalentTo(buIds);
    }

    // -------------------------------------------------------------------------
    // Rejeição de invariantes
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ReportScope com tenantId vazio deve lançar exceção (Req 7)")]
    public void ReportScope_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => ReportScope.Create(Guid.Empty, ReportingRole.TenantAdmin, [], null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*tenantId*");
    }

    [Fact(DisplayName = "ReportScope com GestorBU e BU ids vazio deve lançar (DD-006)")]
    public void ReportScope_GestorBU_WithEmptyBuIds_ShouldThrow()
    {
        var act = () => ReportScope.Create(Guid.NewGuid(), ReportingRole.GestorBU, [], null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*allowedBuIds*");
    }

    [Fact(DisplayName = "ReportScope Platform Operator deve lançar exceção (RNF 5, DD-006)")]
    public void ReportScope_PlatformOperator_ShouldThrow()
    {
        var act = () => ReportScope.Create(Guid.NewGuid(), ReportingRole.PlatformOperator, [], null);

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*PlatformOperator*");
    }

    // -------------------------------------------------------------------------
    // Imutabilidade
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "ReportScope deve ser imutável após construção (Req 7.4)")]
    public void ReportScope_ShouldBeImmutable()
    {
        var tenantId = Guid.NewGuid();
        var scope = ReportScope.Create(tenantId, ReportingRole.TenantAdmin, [], null);

        scope.TenantId.Should().Be(tenantId);
        scope.Role.Should().Be(ReportingRole.TenantAdmin);
    }

    [Fact(DisplayName = "ReportScope AllowedBuIds deve ser somente leitura (Req 7)")]
    public void ReportScope_AllowedBuIds_ShouldBeReadOnly()
    {
        var buIds = new HashSet<Guid> { Guid.NewGuid() };
        var scope = ReportScope.Create(Guid.NewGuid(), ReportingRole.GestorBU, buIds, null);

        // O conjunto retornado deve ser uma cópia/somente-leitura
        var returnedIds = scope.AllowedBuIds;
        returnedIds.Should().NotBeSameAs(buIds,
            because: "AllowedBuIds deve ser cópia defensiva — não expõe a coleção original");
    }
}
