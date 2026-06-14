using FluentAssertions;
using Reporting.Application.Specifications;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Specifications;

/// <summary>
/// Testes unitários para <see cref="RbacScopeSpecification"/>.
///
/// Verifica que cada papel (<see cref="ReportingRole"/>) é traduzido no predicado SQL
/// correto de acordo com o design §4.6 e DD-006:
/// - Vendedor → predicado <c>owner_id = :sub</c>
/// - GestorBU  → predicado <c>bu_id IN (...)</c>
/// - TenantAdmin → predicado vazio (sem restrição adicional além do tenant)
/// - PlatformOperator → lança <see cref="UnauthorizedAccessException"/> (nunca chega ao banco)
///
/// Mapeia: TASK-04, design §4.6, DD-006, Req 7, RNF 5.
/// </summary>
public sealed class RbacScopeSpecificationTests
{
    private readonly RbacScopeSpecification _specification = new();

    // -------------------------------------------------------------------------
    // Vendedor → predicado owner_id = :sub
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Vendedor deve gerar predicado owner_id = :sub (DD-006, Req 7)")]
    public void BuildPredicate_Vendedor_ShouldReturnOwnerIdPredicate()
    {
        var ownerId = Guid.NewGuid();
        var scope = ReportScope.Create(Guid.NewGuid(), ReportingRole.Vendedor, [], ownerId);

        var predicate = _specification.BuildPredicate(scope);

        predicate.Clause.Should().Contain("owner_id = @ownerId",
            because: "Vendedor só enxerga suas próprias oportunidades (Req 2.4, DD-006)");
        predicate.Parameters.Should().ContainKey("ownerId")
            .WhoseValue.Should().Be(ownerId);
    }

    // -------------------------------------------------------------------------
    // GestorBU → predicado bu_id IN (...)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "GestorBU deve gerar predicado bu_id IN (...) (DD-006, Req 7)")]
    public void BuildPredicate_GestorBU_ShouldReturnBuIdInPredicate()
    {
        var buId1 = Guid.NewGuid();
        var buId2 = Guid.NewGuid();
        var buIds = new HashSet<Guid> { buId1, buId2 };
        var scope = ReportScope.Create(Guid.NewGuid(), ReportingRole.GestorBU, buIds, null);

        var predicate = _specification.BuildPredicate(scope);

        predicate.Clause.Should().Contain("bu_id IN @allowedBuIds",
            because: "GestorBU enxerga apenas as BUs de membership (DD-006)");
        predicate.Parameters.Should().ContainKey("allowedBuIds");
        var allowedBuIds = predicate.Parameters["allowedBuIds"] as IEnumerable<Guid>;
        allowedBuIds.Should().BeEquivalentTo(buIds);
    }

    // -------------------------------------------------------------------------
    // TenantAdmin → predicado vazio
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TenantAdmin deve gerar predicado vazio (DD-006, Req 7)")]
    public void BuildPredicate_TenantAdmin_ShouldReturnEmptyPredicate()
    {
        var scope = ReportScope.Create(Guid.NewGuid(), ReportingRole.TenantAdmin, [], null);

        var predicate = _specification.BuildPredicate(scope);

        predicate.IsEmpty.Should().BeTrue(
            because: "TenantAdmin enxerga todo o tenant — sem predicado adicional (DD-006)");
        predicate.Clause.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // PlatformOperator → negação dura antes do banco (RNF 5)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "PlatformOperator deve lançar AccessDeniedException antes de gerar predicado (RNF 5, DD-006)")]
    public void BuildPredicate_PlatformOperator_ShouldThrowAccessDeniedException()
    {
        // PlatformOperator não pode construir ReportScope — mas testamos via ReportScope diretamente
        // ao tentar criar com PlatformOperator, já lança UnauthorizedAccessException
        var act = () => ReportScope.Create(Guid.NewGuid(), ReportingRole.PlatformOperator, [], null);

        act.Should().Throw<UnauthorizedAccessException>(
            because: "PlatformOperator é negação dura — não pode acessar relatórios (RNF 5, DD-006)");
    }

    // -------------------------------------------------------------------------
    // Parâmetros do predicado
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Predicado Vendedor não deve conter parâmetros de BU (DD-006)")]
    public void BuildPredicate_Vendedor_ShouldNotContainBuParameters()
    {
        var scope = ReportScope.Create(Guid.NewGuid(), ReportingRole.Vendedor, [], Guid.NewGuid());

        var predicate = _specification.BuildPredicate(scope);

        predicate.Parameters.Should().NotContainKey("allowedBuIds");
    }

    [Fact(DisplayName = "Predicado GestorBU não deve conter parâmetro de owner (DD-006)")]
    public void BuildPredicate_GestorBU_ShouldNotContainOwnerParameter()
    {
        var buIds = new HashSet<Guid> { Guid.NewGuid() };
        var scope = ReportScope.Create(Guid.NewGuid(), ReportingRole.GestorBU, buIds, null);

        var predicate = _specification.BuildPredicate(scope);

        predicate.Parameters.Should().NotContainKey("ownerId");
    }
}
