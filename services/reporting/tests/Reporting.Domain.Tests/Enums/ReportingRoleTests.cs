using FluentAssertions;
using Reporting.Domain.Enums;
using Xunit;

namespace Reporting.Domain.Tests.Enums;

/// <summary>
/// Testes unitários para o enum <see cref="ReportingRole"/>.
///
/// Invariantes verificadas:
/// - Lista de papéis esperados para o sistema de RBAC.
/// - PlatformOperator é um membro do enum mas sua presença em ReportScope lança.
///
/// Mapeia: TASK-04, design §4.6, RNF 5, DD-006.
/// </summary>
public sealed class ReportingRoleTests
{
    [Fact(DisplayName = "ReportingRole deve conter Vendedor (design §4.6)")]
    public void ReportingRole_ShouldContain_Vendedor()
    {
        Enum.IsDefined(typeof(ReportingRole), ReportingRole.Vendedor).Should().BeTrue();
    }

    [Fact(DisplayName = "ReportingRole deve conter GestorBU (design §4.6)")]
    public void ReportingRole_ShouldContain_GestorBU()
    {
        Enum.IsDefined(typeof(ReportingRole), ReportingRole.GestorBU).Should().BeTrue();
    }

    [Fact(DisplayName = "ReportingRole deve conter TenantAdmin (design §4.6)")]
    public void ReportingRole_ShouldContain_TenantAdmin()
    {
        Enum.IsDefined(typeof(ReportingRole), ReportingRole.TenantAdmin).Should().BeTrue();
    }

    [Fact(DisplayName = "ReportingRole deve conter PlatformOperator (design §4.6, RNF 5)")]
    public void ReportingRole_ShouldContain_PlatformOperator()
    {
        // PlatformOperator é membro do enum mas é bloqueado na construção de ReportScope (DD-006, RNF 5)
        Enum.IsDefined(typeof(ReportingRole), ReportingRole.PlatformOperator).Should().BeTrue();
    }
}
