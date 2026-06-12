using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AuditLog.Domain.Tests.ValueObjects;

/// <summary>
/// Testes de unidade para o objeto de valor <see cref="AuditLogId"/>.
/// </summary>
public sealed class AuditLogIdTests
{
    [Fact(DisplayName = "New cria identificadores únicos a cada chamada")]
    public void New_CriaIdentificadoresUnicos()
    {
        var id1 = AuditLogId.New();
        var id2 = AuditLogId.New();

        id1.Should().NotBe(id2);
    }

    [Fact(DisplayName = "From com Guid válido cria identificador com o valor correto")]
    public void From_GuidValido_CriaIdentificador()
    {
        var guid = Guid.NewGuid();

        var id = AuditLogId.From(guid);

        id.Value.Should().Be(guid);
    }

    [Fact(DisplayName = "From com Guid vazio lança ArgumentException")]
    public void From_GuidVazio_LancaArgumentException()
    {
        var act = () => AuditLogId.From(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Igualdade por valor: mesmo Guid produz instâncias iguais")]
    public void Igualdade_MesmoGuid_Iguais()
    {
        var guid = Guid.NewGuid();

        var id1 = AuditLogId.From(guid);
        var id2 = AuditLogId.From(guid);

        id1.Should().Be(id2);
    }

    [Fact(DisplayName = "Igualdade por valor: Guids distintos produzem instâncias diferentes")]
    public void Igualdade_GuidsDiferentes_Diferentes()
    {
        var id1 = AuditLogId.From(Guid.NewGuid());
        var id2 = AuditLogId.From(Guid.NewGuid());

        id1.Should().NotBe(id2);
    }

    [Fact(DisplayName = "GetHashCode é consistente com a igualdade por valor")]
    public void GetHashCode_ConsistenteComIgualdade()
    {
        var guid = Guid.NewGuid();
        var id1 = AuditLogId.From(guid);
        var id2 = AuditLogId.From(guid);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }
}
