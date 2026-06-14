using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Organization.Domain.Aggregates;
using Organization.Domain.Events;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;
using Xunit;

namespace Organization.Domain.Tests.Aggregates;

/// <summary>
/// Testes unitários e PBTs para o agregado <see cref="BusinessUnit"/>.
/// Cobre: criação, renomeação, desativação, entidades de pipeline (Stage, OriginChannel, LossReason),
/// TerminalStagesPolicy, StagePositionPolicy, BusinessUnitEnablementSpec, seeds.
/// PBT-06: conservação dos estágios terminais em sequências aleatórias.
/// PBT-07: unicidade de nome e position em conjuntos de estágios.
/// </summary>
public sealed class BusinessUnitTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ── Criação ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WhenValidName_ShouldBeActive()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.Active.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseBusinessUnitCreatedEvent()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.DomainEvents.Should().ContainSingle(e => e is BusinessUnitCreated);
    }

    [Fact]
    public void Create_ShouldSetTenantId()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.TenantId.Should().Be(TenantId);
    }

    // ── Rename ───────────────────────────────────────────────────────────────

    [Fact]
    public void Rename_WhenValidName_ShouldUpdateName()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.Rename(BusinessUnitName.Create("Comercial"));
        bu.Name.Value.Should().Be("Comercial");
    }

    [Fact]
    public void Rename_WhenDeactivated_ShouldThrow()
    {
        var bu = CreateWithSeeds();
        bu.Deactivate(DateTimeOffset.UtcNow);
        var act = () => bu.Rename(BusinessUnitName.Create("Outro"));
        act.Should().Throw<DomainException>();
    }

    // ── Deactivate ───────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ShouldSetActiveToFalse()
    {
        var bu = CreateWithSeeds();
        bu.Deactivate(DateTimeOffset.UtcNow);
        bu.Active.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_ShouldRaiseBusinessUnitDeactivatedEvent()
    {
        var bu = CreateWithSeeds();
        bu.ClearDomainEvents();
        bu.Deactivate(DateTimeOffset.UtcNow);
        bu.DomainEvents.Should().ContainSingle(e => e is BusinessUnitDeactivated);
    }

    [Fact]
    public void Deactivate_WhenAlreadyDeactivated_ShouldThrow()
    {
        var bu = CreateWithSeeds();
        bu.Deactivate(DateTimeOffset.UtcNow);
        var act = () => bu.Deactivate(DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }

    // ── Stage: AddStage ───────────────────────────────────────────────────────

    [Fact]
    public void AddStage_WhenValidStage_ShouldBeInStages()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.AddStage("Lead", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        bu.Stages.Should().ContainSingle(s => s.Name == "Lead");
    }

    [Fact]
    public void AddStage_WhenDuplicateName_ShouldThrow()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.AddStage("Lead", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        var act = () => bu.AddStage("Lead", Probability.Create(20), StageCategory.Open, 2, Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-013*");
    }

    [Fact]
    public void AddStage_WhenDuplicatePosition_ShouldThrow()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.AddStage("Lead", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        var act = () => bu.AddStage("Prospecção", Probability.Create(20), StageCategory.Open, 1, Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-014*");
    }

    // ── Stage: RemoveStage ────────────────────────────────────────────────────

    [Fact]
    public void RemoveStage_WhenLastWon_ShouldThrow()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        var wonId = Guid.NewGuid();
        bu.AddStage("Open1", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        bu.AddStage("Ganho", Probability.Create(100), StageCategory.Won, 2, wonId);
        bu.AddStage("Perdido", Probability.Create(0), StageCategory.Lost, 3, Guid.NewGuid());

        var act = () => bu.RemoveStage(wonId);
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-015*");
    }

    [Fact]
    public void RemoveStage_WhenLastLost_ShouldThrow()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        var lostId = Guid.NewGuid();
        bu.AddStage("Open1", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        bu.AddStage("Ganho", Probability.Create(100), StageCategory.Won, 2, Guid.NewGuid());
        bu.AddStage("Perdido", Probability.Create(0), StageCategory.Lost, 3, lostId);

        var act = () => bu.RemoveStage(lostId);
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-015*");
    }

    [Fact]
    public void RemoveStage_WhenOpenWithOtherOpen_ShouldSucceed()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        var openId1 = Guid.NewGuid();
        var openId2 = Guid.NewGuid();
        bu.AddStage("Open1", Probability.Create(10), StageCategory.Open, 1, openId1);
        bu.AddStage("Open2", Probability.Create(20), StageCategory.Open, 2, openId2);
        bu.AddStage("Ganho", Probability.Create(100), StageCategory.Won, 3, Guid.NewGuid());
        bu.AddStage("Perdido", Probability.Create(0), StageCategory.Lost, 4, Guid.NewGuid());

        bu.RemoveStage(openId1);
        bu.Stages.Should().NotContain(s => s.Id == openId1);
    }

    // ── ReorderStages ─────────────────────────────────────────────────────────

    [Fact]
    public void ReorderStages_WhenPositionsAreUnique_ShouldSucceed()
    {
        // Estágios: Lead(1), Prospecção(4), Ganho(10), Perdido(11)
        // Reorder: apenas Lead e Prospecção trocam entre si (1↔4)
        // Resultado: Lead=4, Prospecção=1, Ganho=10, Perdido=11 — sem conflito
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        bu.AddStage("Lead", Probability.Create(10), StageCategory.Open, 1, id1);
        bu.AddStage("Prospecção", Probability.Create(20), StageCategory.Open, 4, id2);
        bu.AddStage("Ganho", Probability.Create(100), StageCategory.Won, 10, Guid.NewGuid());
        bu.AddStage("Perdido", Probability.Create(0), StageCategory.Lost, 11, Guid.NewGuid());

        var newOrder = new Dictionary<Guid, int> { [id1] = 4, [id2] = 1 };
        bu.ReorderStages(newOrder);

        bu.Stages.First(s => s.Id == id2).Position.Should().Be(1);
        bu.Stages.First(s => s.Id == id1).Position.Should().Be(4);
    }

    [Fact]
    public void ReorderStages_WhenDuplicatePositions_ShouldThrow()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        bu.AddStage("Lead", Probability.Create(10), StageCategory.Open, 1, id1);
        bu.AddStage("Prospecção", Probability.Create(20), StageCategory.Open, 2, id2);
        bu.AddStage("Ganho", Probability.Create(100), StageCategory.Won, 3, Guid.NewGuid());
        bu.AddStage("Perdido", Probability.Create(0), StageCategory.Lost, 4, Guid.NewGuid());

        var newOrder = new Dictionary<Guid, int> { [id1] = 1, [id2] = 1 };
        var act = () => bu.ReorderStages(newOrder);
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-014*");
    }

    // ── LossReason / BusinessUnitEnablementSpec ───────────────────────────────

    [Fact]
    public void AddLossReason_WhenValid_ShouldBeInCollection()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.AddLossReason("Preço", Guid.NewGuid());
        bu.LossReasons.Should().ContainSingle(r => r.Name == "Preço");
    }

    [Fact]
    public void DeactivateLossReason_WhenLastActive_ShouldThrow()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        var reasonId = Guid.NewGuid();
        bu.AddLossReason("Preço", reasonId);

        var act = () => bu.DeactivateLossReason(reasonId);
        act.Should().Throw<DomainException>().WithMessage("*ORG-ERR-017*");
    }

    [Fact]
    public void DeactivateLossReason_WhenNotLast_ShouldSucceed()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        bu.AddLossReason("Preço", id1);
        bu.AddLossReason("Prazo", id2);

        bu.DeactivateLossReason(id1);
        bu.LossReasons.First(r => r.Id == id1).Active.Should().BeFalse();
    }

    // ── OriginChannel ─────────────────────────────────────────────────────────

    [Fact]
    public void AddOriginChannel_WhenValid_ShouldBeInCollection()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        bu.AddOriginChannel("Indicação", Guid.NewGuid());
        bu.OriginChannels.Should().ContainSingle(c => c.Name == "Indicação");
    }

    // ── StageSeedFactory ──────────────────────────────────────────────────────

    [Fact]
    public void StageSeedFactory_ShouldContainExactlyOneWon()
    {
        var stages = StageSeedFactory.CreateDefaultStages();
        stages.Count(s => s.Category == StageCategory.Won).Should().Be(1);
    }

    [Fact]
    public void StageSeedFactory_ShouldContainExactlyOneLost()
    {
        var stages = StageSeedFactory.CreateDefaultStages();
        stages.Count(s => s.Category == StageCategory.Lost).Should().Be(1);
    }

    [Fact]
    public void StageSeedFactory_ShouldContainAtLeastOneOpen()
    {
        var stages = StageSeedFactory.CreateDefaultStages();
        stages.Count(s => s.Category == StageCategory.Open).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void StageSeedFactory_ShouldContain8Stages()
    {
        var stages = StageSeedFactory.CreateDefaultStages();
        stages.Should().HaveCount(8);
    }

    [Fact]
    public void StageSeedFactory_PositionsShouldBeUnique()
    {
        var stages = StageSeedFactory.CreateDefaultStages();
        var positions = stages.Select(s => s.Position).ToList();
        positions.Should().OnlyHaveUniqueItems();
    }

    // ── PBT-06: Conservação dos estágios terminais ────────────────────────────

    /// <summary>
    /// PBT-06: gera sequências aleatórias de operações add/remove sobre estágios.
    /// Afirma que o agregado conserva exatamente 1 won, 1 lost e ≥1 open em qualquer estado
    /// alcançável. Operações que violam a política são rejeitadas pelo domínio.
    /// Mínimo de 100 exemplos (MaxTest = 200 para margem de segurança).
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Pbt06_TerminalStagesConservation(PositiveInt stageCount)
    {
        var count = Math.Min(stageCount.Get % 20, 20);
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Pbt06BU"), TenantId, DateTimeOffset.UtcNow);

        // Semente válida: 1 won + 1 lost + N+1 opens (para poder tentar remover alguns)
        var wonId = Guid.NewGuid();
        var lostId = Guid.NewGuid();
        bu.AddStage("Ganho", Probability.Hundred, StageCategory.Won, 100, wonId);
        bu.AddStage("Perdido", Probability.Zero, StageCategory.Lost, 101, lostId);

        var openIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            bu.AddStage($"Open{i}", Probability.Create(i * 10 % 100), StageCategory.Open, 200 + i, id);
            openIds.Add(id);
        }

        // Tenta realizar operações aleatórias: remove opens e tenta remover terminais
        var rng = new Random(stageCount.Get);
        for (var i = 0; i < count; i++)
        {
            // Aleatoriamente tenta remover um estágio (won, lost ou open)
            var allIds = bu.Stages.Select(s => s.Id).ToList();
            if (allIds.Count == 0) break;

            var targetId = allIds[rng.Next(allIds.Count)];
            try { bu.RemoveStage(targetId); }
            catch (DomainException) { /* violação rejeitada pelo domínio */ }
        }

        var wonFinal = bu.Stages.Count(s => s.Category == StageCategory.Won);
        var lostFinal = bu.Stages.Count(s => s.Category == StageCategory.Lost);
        var openFinal = bu.Stages.Count(s => s.Category == StageCategory.Open);

        return (wonFinal == 1 && lostFinal == 1 && openFinal >= 1)
            .ToProperty()
            .Label($"won={wonFinal} lost={lostFinal} open={openFinal}");
    }

    /// <summary>
    /// PBT-06 (complementar): tentativas de remoção dos estágios terminais são sempre rejeitadas.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Pbt06_RemoveLastTerminalAlwaysRejected(PositiveInt seed)
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Pbt06Comp"), TenantId, DateTimeOffset.UtcNow);
        var wonId = Guid.NewGuid();
        var lostId = Guid.NewGuid();
        bu.AddStage("Lead", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());
        bu.AddStage("Ganho", Probability.Hundred, StageCategory.Won, 2, wonId);
        bu.AddStage("Perdido", Probability.Zero, StageCategory.Lost, 3, lostId);

        var rng = new Random(seed.Get);
        var targetId = rng.Next(2) == 0 ? wonId : lostId;

        var threw = false;
        try { bu.RemoveStage(targetId); }
        catch (DomainException) { threw = true; }

        return threw.ToProperty().Label($"targetIsWon={targetId == wonId}");
    }

    // ── PBT-07: Unicidade de nome e position ──────────────────────────────────

    /// <summary>
    /// PBT-07: para qualquer conjunto de nomes e posições distintos adicionados com sucesso,
    /// o agregado conserva unicidade de nome (case-insensitive) e unicidade de position.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Pbt07_StageNameAndPositionUniqueness(NonNegativeInt count)
    {
        var n = Math.Min(count.Get % 20 + 1, 20);
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Pbt07BU"), TenantId, DateTimeOffset.UtcNow);

        var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedPositions = new HashSet<int>();

        for (var i = 0; i < n; i++)
        {
            var name = $"Stage_{i}";
            var position = i + 1;

            if (!addedNames.Contains(name) && !addedPositions.Contains(position))
            {
                bu.AddStage(name, Probability.Create(i % 101), StageCategory.Open, position, Guid.NewGuid());
                addedNames.Add(name);
                addedPositions.Add(position);
            }
        }

        var names = bu.Stages.Select(s => s.Name.ToUpperInvariant()).ToList();
        var positions = bu.Stages.Select(s => s.Position).ToList();

        return (names.Count == names.Distinct().Count() &&
                positions.Count == positions.Distinct().Count())
            .ToProperty()
            .Label($"n={n} names_unique={names.Count == names.Distinct().Count()} pos_unique={positions.Count == positions.Distinct().Count()}");
    }

    /// <summary>
    /// PBT-07 (complementar): tentativa de adicionar nome duplicado sempre lança DomainException.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Pbt07_DuplicateNameAlwaysRejected(PositiveInt seed)
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Pbt07Comp"), TenantId, DateTimeOffset.UtcNow);
        bu.AddStage("Existente", Probability.Create(10), StageCategory.Open, 1, Guid.NewGuid());

        var threw = false;
        try { bu.AddStage("Existente", Probability.Create(20), StageCategory.Open, 2, Guid.NewGuid()); }
        catch (DomainException) { threw = true; }

        return threw.ToProperty().Label("duplicate_name_rejected");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BusinessUnit CreateWithSeeds()
    {
        var bu = BusinessUnit.Create(BusinessUnitName.Create("Vendas"), TenantId, DateTimeOffset.UtcNow);
        var seeds = StageSeedFactory.CreateDefaultStages();
        foreach (var s in seeds)
            bu.AddStage(s.Name, s.Probability, s.Category, s.Position, Guid.NewGuid());
        bu.AddLossReason("Preço", Guid.NewGuid());
        return bu;
    }
}
