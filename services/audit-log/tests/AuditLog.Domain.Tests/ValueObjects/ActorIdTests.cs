using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AuditLog.Domain.Tests.ValueObjects;

/// <summary>
/// Testes de unidade para o objeto de valor <see cref="ActorId"/>.
/// </summary>
public sealed class ActorIdTests
{
    [Fact(DisplayName = "From com Guid válido cria ActorId com o valor correto")]
    public void From_GuidValido_CriaIdentificador()
    {
        var guid = Guid.NewGuid();

        var actorId = ActorId.From(guid);

        actorId.Value.Should().Be(guid);
    }

    [Fact(DisplayName = "From com Guid vazio lança ArgumentException")]
    public void From_GuidVazio_LancaArgumentException()
    {
        var act = () => ActorId.From(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Igualdade por valor: mesmo Guid produz instâncias iguais")]
    public void Igualdade_MesmoGuid_Iguais()
    {
        var guid = Guid.NewGuid();

        var a1 = ActorId.From(guid);
        var a2 = ActorId.From(guid);

        a1.Should().Be(a2);
    }

    [Fact(DisplayName = "Igualdade por valor: Guids distintos produzem instâncias diferentes")]
    public void Igualdade_GuidsDiferentes_Diferentes()
    {
        var a1 = ActorId.From(Guid.NewGuid());
        var a2 = ActorId.From(Guid.NewGuid());

        a1.Should().NotBe(a2);
    }
}
