using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AuditLog.Domain.Tests.ValueObjects;

/// <summary>
/// Testes de unidade para o enum de domínio <see cref="AuditAction"/>.
/// </summary>
public sealed class AuditActionTests
{
    [Fact(DisplayName = "AuditAction possui o valor Create")]
    public void PossuiValorCreate()
    {
        Enum.IsDefined(typeof(AuditAction), AuditAction.Create).Should().BeTrue();
    }

    [Fact(DisplayName = "AuditAction possui o valor Update")]
    public void PossuiValorUpdate()
    {
        Enum.IsDefined(typeof(AuditAction), AuditAction.Update).Should().BeTrue();
    }

    [Fact(DisplayName = "AuditAction possui o valor Delete")]
    public void PossuiValorDelete()
    {
        Enum.IsDefined(typeof(AuditAction), AuditAction.Delete).Should().BeTrue();
    }

    [Fact(DisplayName = "AuditAction possui exatamente 3 valores canônicos")]
    public void PossuiExatamenteTresValores()
    {
        var values = Enum.GetValues<AuditAction>();

        values.Should().HaveCount(3,
            because: "apenas Create, Update e Delete são operações canônicas de auditoria (REQ-002.3)");
    }

    [Fact(DisplayName = "AuditAction do domínio espelha os valores do contrato público")]
    public void EspelamValoresDoContrato()
    {
        var domainValues = Enum.GetNames<AuditAction>();
        var contractValues = Enum.GetNames<AuditLog.Contracts.AuditAction>();

        domainValues.Should().BeEquivalentTo(contractValues,
            because: "o enum de domínio deve ser um espelho semântico do contrato público (TASK-03)");
    }
}
