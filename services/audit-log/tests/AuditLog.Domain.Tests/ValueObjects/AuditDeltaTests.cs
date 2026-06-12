using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AuditLog.Domain.Tests.ValueObjects;

/// <summary>
/// Testes de unidade para o objeto de valor <see cref="AuditDelta"/>.
/// Cobre estrutura das variantes ForCreate/ForUpdate/ForDelete,
/// guards contra valores inválidos e ausência de tipos de ponto flutuante.
/// </summary>
public sealed class AuditDeltaTests
{
    // ------------------------------------------------------------------ ForCreate

    [Fact(DisplayName = "ForCreate com dados válidos cria delta com Kind=Create")]
    public void ForCreate_DadosValidos_KindECreate()
    {
        var after = new Dictionary<string, object?> { ["name"] = "Alice" };

        var delta = AuditDelta.ForCreate(after.AsReadOnly());

        delta.Kind.Should().Be(AuditDeltaKind.Create);
    }

    [Fact(DisplayName = "ForCreate popula After e deixa Before e Changes nulos")]
    public void ForCreate_PopulaAfterDeixaOutrosNulos()
    {
        var after = new Dictionary<string, object?> { ["name"] = "Alice" };

        var delta = AuditDelta.ForCreate(after.AsReadOnly());

        delta.After.Should().NotBeNull();
        delta.Before.Should().BeNull();
        delta.Changes.Should().BeNull();
    }

    [Fact(DisplayName = "ForCreate preserva os pares chave-valor do estado inicial")]
    public void ForCreate_PreservaParesChavalValor()
    {
        var after = new Dictionary<string, object?> { ["stage"] = "Prospect", ["amount"] = 5000L };

        var delta = AuditDelta.ForCreate(after.AsReadOnly());

        delta.After!["stage"].Should().Be("Prospect");
        delta.After!["amount"].Should().Be(5000L);
    }

    [Fact(DisplayName = "ForCreate com dicionário nulo lança ArgumentNullException")]
    public void ForCreate_DicionarioNulo_LancaException()
    {
        var act = () => AuditDelta.ForCreate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "ForCreate com dicionário vazio lança ArgumentException")]
    public void ForCreate_DicionarioVazio_LancaException()
    {
        var act = () => AuditDelta.ForCreate(
            new Dictionary<string, object?>().AsReadOnly());

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ForCreate com valor float lança ArgumentException — money proibido como float")]
    public void ForCreate_ValorFloat_LancaException()
    {
        var after = new Dictionary<string, object?> { ["amount"] = 1.5f };

        var act = () => AuditDelta.ForCreate(after.AsReadOnly());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*float*");
    }

    [Fact(DisplayName = "ForCreate com valor double lança ArgumentException — money proibido como double")]
    public void ForCreate_ValorDouble_LancaException()
    {
        var after = new Dictionary<string, object?> { ["amount"] = 1.5d };

        var act = () => AuditDelta.ForCreate(after.AsReadOnly());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*double*");
    }

    [Fact(DisplayName = "ForCreate com valor decimal lança ArgumentException — money deve ser long (centavos)")]
    public void ForCreate_ValorDecimal_LancaException()
    {
        var after = new Dictionary<string, object?> { ["amount"] = 1.5m };

        var act = () => AuditDelta.ForCreate(after.AsReadOnly());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*decimal*");
    }

    [Fact(DisplayName = "ForCreate aceita valores monetários como long (centavos inteiros)")]
    public void ForCreate_ValorLong_Aceita()
    {
        var after = new Dictionary<string, object?> { ["amount_cents"] = 150_00L };

        var act = () => AuditDelta.ForCreate(after.AsReadOnly());

        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------ ForUpdate

    [Fact(DisplayName = "ForUpdate com mudanças válidas cria delta com Kind=Update")]
    public void ForUpdate_MudancasValidas_KindEUpdate()
    {
        var changes = new Dictionary<string, AuditAttributeChange>
        {
            ["stage"] = new AuditAttributeChange("Prospect", "Qualified")
        };

        var delta = AuditDelta.ForUpdate(changes.AsReadOnly());

        delta.Kind.Should().Be(AuditDeltaKind.Update);
    }

    [Fact(DisplayName = "ForUpdate popula Changes e deixa After e Before nulos")]
    public void ForUpdate_PopulaChangesDeixaOutrosNulos()
    {
        var changes = new Dictionary<string, AuditAttributeChange>
        {
            ["stage"] = new AuditAttributeChange("A", "B")
        };

        var delta = AuditDelta.ForUpdate(changes.AsReadOnly());

        delta.Changes.Should().NotBeNull();
        delta.After.Should().BeNull();
        delta.Before.Should().BeNull();
    }

    [Fact(DisplayName = "ForUpdate preserva os pares before/after para cada atributo alterado")]
    public void ForUpdate_PreservaParesBefforeAfter()
    {
        var changes = new Dictionary<string, AuditAttributeChange>
        {
            ["stage"] = new AuditAttributeChange("Prospect", "Qualified"),
            ["amount_cents"] = new AuditAttributeChange(1000L, 2000L)
        };

        var delta = AuditDelta.ForUpdate(changes.AsReadOnly());

        delta.Changes!["stage"].Before.Should().Be("Prospect");
        delta.Changes!["stage"].After.Should().Be("Qualified");
        delta.Changes!["amount_cents"].Before.Should().Be(1000L);
        delta.Changes!["amount_cents"].After.Should().Be(2000L);
    }

    [Fact(DisplayName = "ForUpdate com dicionário nulo lança ArgumentNullException")]
    public void ForUpdate_DicionarioNulo_LancaException()
    {
        var act = () => AuditDelta.ForUpdate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "ForUpdate com dicionário vazio lança ArgumentException")]
    public void ForUpdate_DicionarioVazio_LancaException()
    {
        var act = () => AuditDelta.ForUpdate(
            new Dictionary<string, AuditAttributeChange>().AsReadOnly());

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ForUpdate rejeita atributo onde before == after (não foi alterado)")]
    public void ForUpdate_AtributoNaoAlterado_LancaException()
    {
        var changes = new Dictionary<string, AuditAttributeChange>
        {
            ["stage"] = new AuditAttributeChange("Prospect", "Prospect")  // antes == depois
        };

        var act = () => AuditDelta.ForUpdate(changes.AsReadOnly());

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ForUpdate com valor double em Before lança ArgumentException")]
    public void ForUpdate_ValorDoubleEmBefore_LancaException()
    {
        var changes = new Dictionary<string, AuditAttributeChange>
        {
            ["amount"] = new AuditAttributeChange(1.5d, 2.5d)
        };

        var act = () => AuditDelta.ForUpdate(changes.AsReadOnly());

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ForUpdate com valor float em After lança ArgumentException")]
    public void ForUpdate_ValorFloatEmAfter_LancaException()
    {
        var changes = new Dictionary<string, AuditAttributeChange>
        {
            ["amount"] = new AuditAttributeChange(null, 1.5f)
        };

        var act = () => AuditDelta.ForUpdate(changes.AsReadOnly());

        act.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------ ForDelete

    [Fact(DisplayName = "ForDelete com dados válidos cria delta com Kind=Delete")]
    public void ForDelete_DadosValidos_KindEDelete()
    {
        var before = new Dictionary<string, object?> { ["name"] = "Alice" };

        var delta = AuditDelta.ForDelete(before.AsReadOnly());

        delta.Kind.Should().Be(AuditDeltaKind.Delete);
    }

    [Fact(DisplayName = "ForDelete popula Before e deixa After e Changes nulos")]
    public void ForDelete_PopulaBeforeDeixaOutrosNulos()
    {
        var before = new Dictionary<string, object?> { ["name"] = "Alice" };

        var delta = AuditDelta.ForDelete(before.AsReadOnly());

        delta.Before.Should().NotBeNull();
        delta.After.Should().BeNull();
        delta.Changes.Should().BeNull();
    }

    [Fact(DisplayName = "ForDelete preserva os pares chave-valor do último estado conhecido")]
    public void ForDelete_PreservaParesChavalValor()
    {
        var before = new Dictionary<string, object?> { ["name"] = "Bob", ["amount_cents"] = 9999L };

        var delta = AuditDelta.ForDelete(before.AsReadOnly());

        delta.Before!["name"].Should().Be("Bob");
        delta.Before!["amount_cents"].Should().Be(9999L);
    }

    [Fact(DisplayName = "ForDelete com dicionário nulo lança ArgumentNullException")]
    public void ForDelete_DicionarioNulo_LancaException()
    {
        var act = () => AuditDelta.ForDelete(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "ForDelete com dicionário vazio lança ArgumentException")]
    public void ForDelete_DicionarioVazio_LancaException()
    {
        var act = () => AuditDelta.ForDelete(
            new Dictionary<string, object?>().AsReadOnly());

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ForDelete com valor double lança ArgumentException — money proibido")]
    public void ForDelete_ValorDouble_LancaException()
    {
        var before = new Dictionary<string, object?> { ["amount"] = 1.5d };

        var act = () => AuditDelta.ForDelete(before.AsReadOnly());

        act.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------ Igualdade por valor

    [Fact(DisplayName = "Dois ForCreate com o mesmo conteúdo são iguais por valor")]
    public void ForCreate_MesmoConteudo_Iguais()
    {
        var guid = Guid.NewGuid();
        var d1 = AuditDelta.ForCreate(
            new Dictionary<string, object?> { ["id"] = guid, ["name"] = "Alice" }.AsReadOnly());
        var d2 = AuditDelta.ForCreate(
            new Dictionary<string, object?> { ["id"] = guid, ["name"] = "Alice" }.AsReadOnly());

        d1.Should().Be(d2);
    }

    [Fact(DisplayName = "Dois ForDelete com o mesmo conteúdo são iguais por valor")]
    public void ForDelete_MesmoConteudo_Iguais()
    {
        var d1 = AuditDelta.ForDelete(
            new Dictionary<string, object?> { ["name"] = "Bob" }.AsReadOnly());
        var d2 = AuditDelta.ForDelete(
            new Dictionary<string, object?> { ["name"] = "Bob" }.AsReadOnly());

        d1.Should().Be(d2);
    }

    [Fact(DisplayName = "Dois ForUpdate com as mesmas mudanças são iguais por valor")]
    public void ForUpdate_MesmasMudancas_Iguais()
    {
        var d1 = AuditDelta.ForUpdate(
            new Dictionary<string, AuditAttributeChange>
            {
                ["stage"] = new AuditAttributeChange("A", "B")
            }.AsReadOnly());
        var d2 = AuditDelta.ForUpdate(
            new Dictionary<string, AuditAttributeChange>
            {
                ["stage"] = new AuditAttributeChange("A", "B")
            }.AsReadOnly());

        d1.Should().Be(d2);
    }

    [Fact(DisplayName = "Deltas com Kind diferente não são iguais")]
    public void Deltas_KindDiferente_NaoIguais()
    {
        var d1 = AuditDelta.ForCreate(new Dictionary<string, object?> { ["x"] = 1L }.AsReadOnly());
        var d2 = AuditDelta.ForDelete(new Dictionary<string, object?> { ["x"] = 1L }.AsReadOnly());

        d1.Should().NotBe(d2);
    }
}
