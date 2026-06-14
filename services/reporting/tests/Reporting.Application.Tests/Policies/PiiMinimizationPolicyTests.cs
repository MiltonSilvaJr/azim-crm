using FluentAssertions;
using Reporting.Application.Policies;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Policies;

/// <summary>
/// Testes da <see cref="PiiMinimizationPolicy"/> — TASK-08.
/// Verifica inclusão/omissão de display_name por papel (DD-008, RNF 4).
/// </summary>
public sealed class PiiMinimizationPolicyTests
{
    private readonly PiiMinimizationPolicy _policy = new();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId  = Guid.NewGuid();

    [Fact(DisplayName = "Vendedor: IncludeDisplayName=true (pode ver o próprio nome)")]
    public void IncludeDisplayName_Vendedor_ReturnsTrue()
    {
        var scope = ReportScope.Create(TenantId, ReportingRole.Vendedor, [], OwnerId);
        _policy.IncludeDisplayName(scope).Should().BeTrue();
    }

    [Fact(DisplayName = "GestorBU: IncludeDisplayName=true (pode ver nomes no escopo de gestão)")]
    public void IncludeDisplayName_GestorBu_ReturnsTrue()
    {
        var scope = ReportScope.Create(TenantId, ReportingRole.GestorBU, [Guid.NewGuid()], null);
        _policy.IncludeDisplayName(scope).Should().BeTrue();
    }

    [Fact(DisplayName = "TenantAdmin: IncludeDisplayName=true (pode ver todos os nomes do tenant)")]
    public void IncludeDisplayName_TenantAdmin_ReturnsTrue()
    {
        var scope = ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);
        _policy.IncludeDisplayName(scope).Should().BeTrue();
    }

    [Fact(DisplayName = "ApplyTo: retorna displayName quando política permite")]
    public void ApplyTo_WhenAllowed_ReturnsDisplayName()
    {
        var scope = ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);
        var result = _policy.ApplyTo("João Silva", scope);
        result.Should().Be("João Silva");
    }

    [Fact(DisplayName = "ApplyTo: retorna null quando displayName é null (sem substituição)")]
    public void ApplyTo_WhenDisplayNameIsNull_ReturnsNull()
    {
        var scope = ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);
        var result = _policy.ApplyTo(null, scope);
        result.Should().BeNull();
    }
}
