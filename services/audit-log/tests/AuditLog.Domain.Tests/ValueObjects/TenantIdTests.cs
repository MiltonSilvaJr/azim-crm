using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AuditLog.Domain.Tests.ValueObjects;

/// <summary>
/// Testes de unidade para o objeto de valor <see cref="TenantId"/>.
/// </summary>
public sealed class TenantIdTests
{
    [Fact(DisplayName = "From com Guid válido cria TenantId com o valor correto")]
    public void From_GuidValido_CriaIdentificador()
    {
        var guid = Guid.NewGuid();

        var tenantId = TenantId.From(guid);

        tenantId.Value.Should().Be(guid);
    }

    [Fact(DisplayName = "From com Guid vazio lança ArgumentException")]
    public void From_GuidVazio_LancaArgumentException()
    {
        var act = () => TenantId.From(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Igualdade por valor: mesmo Guid produz instâncias iguais")]
    public void Igualdade_MesmoGuid_Iguais()
    {
        var guid = Guid.NewGuid();

        var t1 = TenantId.From(guid);
        var t2 = TenantId.From(guid);

        t1.Should().Be(t2);
    }

    [Fact(DisplayName = "Igualdade por valor: Guids distintos produzem instâncias diferentes")]
    public void Igualdade_GuidsDiferentes_Diferentes()
    {
        var t1 = TenantId.From(Guid.NewGuid());
        var t2 = TenantId.From(Guid.NewGuid());

        t1.Should().NotBe(t2);
    }
}
