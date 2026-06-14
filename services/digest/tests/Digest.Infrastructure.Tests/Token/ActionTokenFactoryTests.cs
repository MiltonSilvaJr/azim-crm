using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Clock;
using Digest.Infrastructure.Token;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Xunit;

namespace Digest.Infrastructure.Tests.Token;

/// <summary>
/// Testes unitários e PBT-05 para <see cref="ActionTokenFactory"/>.
/// PBT-05: tokens distintos, não sequenciais, não deriváveis; hash 32 bytes;
/// Base64Url válido; não inferíveis entre invocações (RNF 7.2, ADR-0006).
/// </summary>
public sealed class ActionTokenFactoryTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 14, 10, 0, 0, TimeSpan.Zero);

    private static ActionTokenFactory CreateFactory(DateTimeOffset? now = null)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now ?? FixedNow);
        return new ActionTokenFactory(clock);
    }

    // ---------------------------------------------------------------
    // Testes unitários nominais
    // ---------------------------------------------------------------

    [Fact(DisplayName = "IssueAsync retorna ActionToken com ClearToken não nulo")]
    public async Task IssueAsync_Returns_NonNullClearToken()
    {
        var factory = CreateFactory();
        var token = await factory.IssueAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ActionType.Complete);

        token.ClearToken.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "IssueAsync retorna ActionToken com TokenHash de 32 bytes (SHA-256)")]
    public async Task IssueAsync_Returns_HashOf32Bytes()
    {
        var factory = CreateFactory();
        var token = await factory.IssueAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ActionType.Reschedule);

        token.TokenHash.Should().HaveCount(32, "SHA-256 produz 32 bytes");
    }

    [Fact(DisplayName = "IssueAsync ClearToken tem 43 caracteres (Base64Url de 32 bytes sem padding)")]
    public async Task IssueAsync_ClearToken_Is43Chars()
    {
        var factory = CreateFactory();
        var token = await factory.IssueAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ActionType.Complete);

        // 32 bytes → Base64 = 44 chars → TrimEnd('=') = 43 chars (sem padding)
        token.ClearToken.Should().HaveLength(43);
    }

    [Fact(DisplayName = "IssueAsync ClearToken não contém +, / ou = (Base64Url)")]
    public async Task IssueAsync_ClearToken_IsBase64Url()
    {
        var factory = CreateFactory();
        var token = await factory.IssueAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ActionType.Complete);

        token.ClearToken.Should().NotContainAny("+", "/", "=");
    }

    [Fact(DisplayName = "IssueAsync com tenant_id vazio lança ArgumentException")]
    public async Task IssueAsync_EmptyTenantId_Throws()
    {
        var factory = CreateFactory();
        var act = async () => await factory.IssueAsync(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), ActionType.Complete);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "10.000 emissões produzem tokens distintos (sanity check de entropia CSPRNG)")]
    public async Task IssueAsync_10000_Tokens_AreUnique()
    {
        var factory = CreateFactory();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tokens = new HashSet<string>();
        for (int i = 0; i < 10_000; i++)
        {
            var token = await factory.IssueAsync(tenantId, userId, Guid.NewGuid(), ActionType.Complete);
            tokens.Add(token.ClearToken);
        }

        tokens.Should().HaveCount(10_000, "nenhum token deve ser repetido em 10.000 emissões (entropia CSPRNG)");
    }

    // ---------------------------------------------------------------
    // PBT-05 — tokens distintos, não sequenciais, não deriváveis
    // Padrão FsCheck v2: [Property] com parâmetros gerados automaticamente,
    // retornando Property a partir de bool.ToProperty().
    // ---------------------------------------------------------------

    /// <summary>
    /// PBT-05: dois tokens para o mesmo input são sempre diferentes (não determinístico — CSPRNG).
    /// Usa PositiveInt como seed arbitrário para forçar N iterações via FsCheck v2.
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-05: mesmo input produz tokens sempre diferentes (CSPRNG não determinístico)")]
    public Property Pbt05_SameInput_AlwaysProducesDifferentTokens(PositiveInt _)
    {
        var factory = CreateFactory();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var t1 = factory.IssueAsync(tenantId, userId, activityId, ActionType.Complete).GetAwaiter().GetResult();
        var t2 = factory.IssueAsync(tenantId, userId, activityId, ActionType.Complete).GetAwaiter().GetResult();

        return (t1.ClearToken != t2.ClearToken)
            .ToProperty()
            .Label("CSPRNG deve gerar tokens distintos mesmo para o mesmo input");
    }

    /// <summary>
    /// PBT-05: ClearToken sempre é Base64Url válido (somente alfanumérico, '-', '_').
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-05: ClearToken sempre é Base64Url válido (sem +, /, =)")]
    public Property Pbt05_ClearToken_AlwaysValidBase64Url(PositiveInt _)
    {
        var factory = CreateFactory();
        var token = factory.IssueAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ActionType.Reschedule)
            .GetAwaiter().GetResult();

        var isValid = token.ClearToken.All(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_');

        return isValid.ToProperty()
            .Label($"ClearToken={token.ClearToken} deve ser Base64Url válido");
    }

    /// <summary>
    /// PBT-05: TokenHash sempre tem exatamente 32 bytes (SHA-256).
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-05: TokenHash sempre tem 32 bytes (SHA-256)")]
    public Property Pbt05_TokenHash_Always32Bytes(PositiveInt _)
    {
        var factory = CreateFactory();
        var token = factory.IssueAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ActionType.Complete)
            .GetAwaiter().GetResult();

        return (token.TokenHash.Length == 32).ToProperty()
            .Label($"TokenHash.Length={token.TokenHash.Length} deve ser 32");
    }

    /// <summary>
    /// PBT-05: N emissões para atividades distintas produzem N tokens distintos (unicidade).
    /// N gerado como (seed % 9) + 2 para manter no intervalo [2..10].
    /// </summary>
    [Property(MaxTest = 100, DisplayName = "PBT-05: N emissões para atividades distintas são todas diferentes")]
    public Property Pbt05_NActivities_AllTokensDistinct(PositiveInt seed)
    {
        var n = (seed.Get % 9) + 2; // range [2..10]
        var factory = CreateFactory();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tokens = Enumerable.Range(0, n)
            .Select(_ => factory.IssueAsync(tenantId, userId, Guid.NewGuid(), ActionType.Complete)
                .GetAwaiter().GetResult())
            .ToList();

        var distinctClear = tokens.Select(t => t.ClearToken).Distinct().Count();
        var distinctHashes = tokens.Select(t => Convert.ToHexString(t.TokenHash)).Distinct().Count();

        return (distinctClear == n && distinctHashes == n)
            .ToProperty()
            .Label($"N={n}: clear={distinctClear} distinct, hash={distinctHashes} distinct");
    }
}
