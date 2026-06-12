using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AuditLog.Domain.Tests.ValueObjects;

/// <summary>
/// Testes de unidade para o objeto de valor <see cref="EntityReference"/>.
/// </summary>
public sealed class EntityReferenceTests
{
    [Fact(DisplayName = "Criação com argumentos válidos produz instância correta")]
    public void Create_ArgumentosValidos_CriaInstancia()
    {
        var entityId = Guid.NewGuid();

        var reference = EntityReference.Create("Opportunity", entityId);

        reference.EntityType.Should().Be("Opportunity");
        reference.EntityId.Should().Be(entityId);
    }

    [Fact(DisplayName = "Criação com EntityType nulo lança ArgumentNullException")]
    public void Create_EntityTypeNulo_LancaException()
    {
        var act = () => EntityReference.Create(null!, Guid.NewGuid());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Criação com EntityType vazio lança ArgumentException")]
    public void Create_EntityTypeVazio_LancaException()
    {
        var act = () => EntityReference.Create(string.Empty, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Criação com EntityType com mais de 50 caracteres lança ArgumentException")]
    public void Create_EntityTypeLongoDemais_LancaException()
    {
        var longType = new string('X', 51);

        var act = () => EntityReference.Create(longType, Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Criação com EntityType de exatamente 50 caracteres é aceita")]
    public void Create_EntityTypeCom50Chars_Aceita()
    {
        var exactly50 = new string('X', 50);

        var act = () => EntityReference.Create(exactly50, Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Criação com EntityId vazio lança ArgumentException")]
    public void Create_EntityIdVazio_LancaException()
    {
        var act = () => EntityReference.Create("Contact", Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Igualdade por valor: mesmos atributos produzem instâncias iguais")]
    public void Igualdade_MesmoAtributos_Iguais()
    {
        var entityId = Guid.NewGuid();

        var ref1 = EntityReference.Create("Contact", entityId);
        var ref2 = EntityReference.Create("Contact", entityId);

        ref1.Should().Be(ref2);
    }

    [Fact(DisplayName = "Igualdade por valor: atributos distintos produzem instâncias diferentes")]
    public void Igualdade_AtributosDiferentes_Diferentes()
    {
        var ref1 = EntityReference.Create("Contact", Guid.NewGuid());
        var ref2 = EntityReference.Create("Account", Guid.NewGuid());

        ref1.Should().NotBe(ref2);
    }
}
