using FluentAssertions;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.ValueObjects;
using Xunit;

namespace GoalForecast.Domain.Tests.ValueObjects;

/// <summary>
/// Testes do objeto de valor <see cref="GoalScope"/> (BU ou RESPONSAVEL).
/// Cobre TASK-04: INV-3, igualdade por valor, Kind derivado.
/// Mapeia: Req 1.3, requirements §4, INV-3, design §4.3.
/// </summary>
public sealed class GoalScopeTests
{
    private static readonly Guid ValidBuId = Guid.NewGuid();
    private static readonly Guid ValidOwnerId = Guid.NewGuid();

    // =========================================================================
    // Escopo BU
    // =========================================================================

    [Fact(DisplayName = "GoalScope.ForBu cria escopo BU com Kind == BU")]
    public void ForBu_ValidBuId_CreatesWithKindBu()
    {
        var scope = GoalScope.ForBu(ValidBuId);

        scope.Kind.Should().Be(GoalScopeKind.BU);
        scope.BuId.Should().Be(ValidBuId);
        scope.OwnerId.Should().BeNull();
    }

    [Fact(DisplayName = "GoalScope.ForBu com BuId vazio (Guid.Empty) lança DomainException GF-ERR-003")]
    public void ForBu_EmptyBuId_ThrowsDomainException()
    {
        var act = () => GoalScope.ForBu(Guid.Empty);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-003");
    }

    // =========================================================================
    // Escopo RESPONSAVEL
    // =========================================================================

    [Fact(DisplayName = "GoalScope.ForResponsavel cria escopo RESPONSAVEL com Kind == RESPONSAVEL")]
    public void ForResponsavel_ValidIds_CreatesWithKindResponsavel()
    {
        var scope = GoalScope.ForResponsavel(ValidBuId, ValidOwnerId);

        scope.Kind.Should().Be(GoalScopeKind.RESPONSAVEL);
        scope.BuId.Should().Be(ValidBuId);
        scope.OwnerId.Should().Be(ValidOwnerId);
    }

    [Fact(DisplayName = "GoalScope.ForResponsavel com BuId vazio lança DomainException GF-ERR-003")]
    public void ForResponsavel_EmptyBuId_ThrowsDomainException()
    {
        var act = () => GoalScope.ForResponsavel(Guid.Empty, ValidOwnerId);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-003");
    }

    [Fact(DisplayName = "GoalScope.ForResponsavel com OwnerId vazio lança DomainException GF-ERR-003")]
    public void ForResponsavel_EmptyOwnerId_ThrowsDomainException()
    {
        var act = () => GoalScope.ForResponsavel(ValidBuId, Guid.Empty);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GF-ERR-003");
    }

    // =========================================================================
    // INV-3: BU não pode ter OwnerId; RESPONSAVEL deve ter OwnerId
    // =========================================================================

    [Fact(DisplayName = "GoalScope BU não tem OwnerId (INV-3)")]
    public void ForBu_OwnerId_IsNull()
    {
        var scope = GoalScope.ForBu(ValidBuId);

        scope.OwnerId.Should().BeNull();
    }

    [Fact(DisplayName = "GoalScope RESPONSAVEL tem OwnerId (INV-3)")]
    public void ForResponsavel_OwnerId_IsNotNull()
    {
        var scope = GoalScope.ForResponsavel(ValidBuId, ValidOwnerId);

        scope.OwnerId.Should().NotBeNull();
        scope.OwnerId.Should().Be(ValidOwnerId);
    }

    // =========================================================================
    // Kind derivado
    // =========================================================================

    [Fact(DisplayName = "Kind é BU quando OwnerId é nulo")]
    public void Kind_WhenOwnerIdNull_IsBu()
    {
        var scope = GoalScope.ForBu(ValidBuId);
        scope.Kind.Should().Be(GoalScopeKind.BU);
    }

    [Fact(DisplayName = "Kind é RESPONSAVEL quando OwnerId está presente")]
    public void Kind_WhenOwnerIdPresent_IsResponsavel()
    {
        var scope = GoalScope.ForResponsavel(ValidBuId, ValidOwnerId);
        scope.Kind.Should().Be(GoalScopeKind.RESPONSAVEL);
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "Dois GoalScope BU com mesmo BuId são iguais")]
    public void Equality_SameBuId_AreEqual()
    {
        var buId = Guid.NewGuid();
        var a = GoalScope.ForBu(buId);
        var b = GoalScope.ForBu(buId);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact(DisplayName = "Dois GoalScope RESPONSAVEL com mesmos ids são iguais")]
    public void Equality_SameIds_AreEqual()
    {
        var buId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var a = GoalScope.ForResponsavel(buId, ownerId);
        var b = GoalScope.ForResponsavel(buId, ownerId);

        a.Should().Be(b);
    }

    [Fact(DisplayName = "GoalScope BU e RESPONSAVEL com mesmo BuId não são iguais")]
    public void Equality_BuVsResponsavel_NotEqual()
    {
        var buId = Guid.NewGuid();
        var a = GoalScope.ForBu(buId);
        var b = GoalScope.ForResponsavel(buId, ValidOwnerId);

        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "GoalScope BU com BuIds diferentes não são iguais")]
    public void Equality_DifferentBuIds_NotEqual()
    {
        var a = GoalScope.ForBu(Guid.NewGuid());
        var b = GoalScope.ForBu(Guid.NewGuid());

        a.Should().NotBe(b);
    }
}
