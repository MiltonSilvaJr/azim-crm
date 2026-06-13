using Authentication.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Authentication.Domain.Tests.ValueObjects;

/// <summary>
/// Testes unitários de <see cref="MembershipSet"/>.
///
/// Mapeia: Req 5.5 (cache de memberships), design.md § 4.3.
/// Critérios: imutabilidade, igualdade por valor, ausência de duplicatas por bu_id,
/// rastreabilidade de cached_at para TTL.
/// </summary>
public sealed class MembershipSetTests
{
    // =========================================================================
    // Criação e invariantes
    // =========================================================================

    [Fact(DisplayName = "MembershipSet vazio deve ser criado com sucesso")]
    public void Create_Empty_ShouldSucceed()
    {
        var cachedAt = DateTimeOffset.UtcNow;
        var set = MembershipSet.Create([], cachedAt);
        set.Entries.Should().BeEmpty();
        set.CachedAt.Should().Be(cachedAt);
    }

    [Fact(DisplayName = "MembershipSet deve rejeitar entradas duplicadas por bu_id")]
    public void Create_WithDuplicateBuId_ShouldThrow()
    {
        var buId = Guid.NewGuid();
        var entries = new[]
        {
            new MembershipEntry(buId, "admin"),
            new MembershipEntry(buId, "viewer"),
        };

        var act = () => MembershipSet.Create(entries, DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>("bu_id duplicado deve ser rejeitado");
    }

    [Fact(DisplayName = "MembershipSet deve preservar entradas com bu_ids distintos")]
    public void Create_WithDistinctBuIds_ShouldPreserveAllEntries()
    {
        var entries = new[]
        {
            new MembershipEntry(Guid.NewGuid(), "admin"),
            new MembershipEntry(Guid.NewGuid(), "viewer"),
        };

        var set = MembershipSet.Create(entries, DateTimeOffset.UtcNow);
        set.Entries.Should().HaveCount(2);
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    [Fact(DisplayName = "Dois MembershipSet com mesmos dados devem ser iguais")]
    public void Equality_SameData_ShouldBeEqual()
    {
        var buId = Guid.NewGuid();
        var cachedAt = DateTimeOffset.UtcNow;

        var a = MembershipSet.Create([new MembershipEntry(buId, "admin")], cachedAt);
        var b = MembershipSet.Create([new MembershipEntry(buId, "admin")], cachedAt);

        a.Should().Be(b);
    }

    [Fact(DisplayName = "MembershipSet com entradas diferentes não deve ser igual")]
    public void Equality_DifferentEntries_ShouldNotBeEqual()
    {
        var cachedAt = DateTimeOffset.UtcNow;
        var a = MembershipSet.Create([new MembershipEntry(Guid.NewGuid(), "admin")], cachedAt);
        var b = MembershipSet.Create([new MembershipEntry(Guid.NewGuid(), "viewer")], cachedAt);

        a.Should().NotBe(b);
    }

    // =========================================================================
    // Imutabilidade
    // =========================================================================

    [Fact(DisplayName = "MembershipSet não deve expor setter público em Entries")]
    public void Immutability_Entries_ShouldHaveNoPublicSetter()
    {
        var prop = typeof(MembershipSet).GetProperty(nameof(MembershipSet.Entries));
        prop.Should().NotBeNull();
        prop!.SetMethod?.IsPublic.Should().BeFalse("Entries deve ser somente leitura");
    }

    [Fact(DisplayName = "MembershipSet não deve expor setter público em CachedAt")]
    public void Immutability_CachedAt_ShouldHaveNoPublicSetter()
    {
        var prop = typeof(MembershipSet).GetProperty(nameof(MembershipSet.CachedAt));
        prop.Should().NotBeNull();
        prop!.SetMethod?.IsPublic.Should().BeFalse("CachedAt deve ser somente leitura");
    }
}
