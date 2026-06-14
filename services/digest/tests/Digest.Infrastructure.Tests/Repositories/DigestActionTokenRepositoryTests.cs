using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Repositories;
using Digest.Infrastructure.Tests.Fixtures;
using Digest.Infrastructure.Token;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using InfraClock = Digest.Infrastructure.Clock.IClock;

namespace Digest.Infrastructure.Tests.Repositories;

/// <summary>
/// Testes de integração para <see cref="DigestActionTokenRepository"/> (TASK-17).
/// Verifica persistência do token_hash (SHA-256) e recuperação por hash.
/// Token em claro nunca é armazenado nem logado (DD-007, DD-011).
/// Usa Testcontainers + PostgreSQL real (compartilhado via collection "Postgres").
/// </summary>
[Collection("Postgres")]
public sealed class DigestActionTokenRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 14, 10, 0, 0, TimeSpan.Zero);

    public DigestActionTokenRepositoryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private ActionTokenFactory CreateTokenFactory()
    {
        var clock = Substitute.For<InfraClock>();
        clock.UtcNow.Returns(FixedNow);
        return new ActionTokenFactory(clock);
    }

    // ---------------------------------------------------------------
    // IssueTokenAsync — caminho feliz
    // ---------------------------------------------------------------

    [Fact(DisplayName = "IssueTokenAsync persiste o DigestActionToken com token_hash correto")]
    public async Task IssueTokenAsync_PersistsTokenHash()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var actionToken = await CreateTokenFactory()
            .IssueAsync(tenantId, userId, activityId, ActionType.Complete);

        var entity = DigestActionToken.Issue(tenantId, userId, activityId, ActionType.Complete, actionToken);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new DigestActionTokenRepository(ctx);

        var beforeIssue = DateTimeOffset.UtcNow;

        // Act
        await repo.IssueTokenAsync(entity);

        var afterIssue = DateTimeOffset.UtcNow;

        // Assert: verifica via contexto separado
        await using var verifyCtx = _fixture.CreateDbContext(tenantId);
        var persisted = await verifyCtx.DigestActionTokens
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ActivityId == activityId);

        persisted.Should().NotBeNull();
        persisted!.TokenHash.Should().Equal(actionToken.TokenHash,
            "somente o hash SHA-256 deve ser persistido (DD-007)");

        // ExpiresAt = createdAt + 48h — createdAt é DateTimeOffset.UtcNow no momento do Issue
        // Verifica que ExpiresAt está no intervalo [beforeIssue + 48h, afterIssue + 48h]
        persisted.ExpiresAt.Should().BeOnOrAfter(beforeIssue.AddHours(48).AddSeconds(-5));
        persisted.ExpiresAt.Should().BeOnOrBefore(afterIssue.AddHours(48).AddSeconds(5));
    }

    [Fact(DisplayName = "IssueTokenAsync persiste token_hash e GetByHashAsync o recupera")]
    public async Task IssueTokenAsync_AndGetByHash_RoundTrip()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var actionToken = await CreateTokenFactory()
            .IssueAsync(tenantId, userId, activityId, ActionType.Reschedule);

        var entity = DigestActionToken.Issue(tenantId, userId, activityId, ActionType.Reschedule, actionToken);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new DigestActionTokenRepository(ctx);
        await repo.IssueTokenAsync(entity);

        // Act: busca pelo hash
        await using var readCtx = _fixture.CreateDbContext(tenantId);
        var readRepo = new DigestActionTokenRepository(readCtx);
        var found = await readRepo.GetByHashAsync(tenantId, actionToken.TokenHash);

        found.Should().NotBeNull("o token deve ser encontrado pelo hash SHA-256");
        found!.TenantId.Should().Be(tenantId);
        found.UserId.Should().Be(userId);
        found.ActivityId.Should().Be(activityId);
        found.Action.Should().Be(ActionType.Reschedule);
        found.TokenHash.Should().Equal(actionToken.TokenHash);
    }

    [Fact(DisplayName = "GetByHashAsync retorna null para hash inexistente")]
    public async Task GetByHashAsync_UnknownHash_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var unknownHash = new byte[32]; // zeros — nunca emitido

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var repo = new DigestActionTokenRepository(ctx);

        var result = await repo.GetByHashAsync(tenantId, unknownHash);

        result.Should().BeNull("hash inexistente não deve retornar resultado");
    }

    [Fact(DisplayName = "GetByHashAsync para tenant diferente retorna null (Global Query Filter)")]
    public async Task GetByHashAsync_DifferentTenant_ReturnsNull()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var actionToken = await CreateTokenFactory()
            .IssueAsync(tenantA, userId, activityId, ActionType.Complete);

        var entity = DigestActionToken.Issue(tenantA, userId, activityId, ActionType.Complete, actionToken);

        // Persiste com tenant A
        await using var ctxA = _fixture.CreateDbContext(tenantA);
        var repoA = new DigestActionTokenRepository(ctxA);
        await repoA.IssueTokenAsync(entity);

        // Tenta buscar com tenant B — Global Query Filter deve impedir
        await using var ctxB = _fixture.CreateDbContext(tenantB);
        var repoB = new DigestActionTokenRepository(ctxB);
        var result = await repoB.GetByHashAsync(tenantB, actionToken.TokenHash);

        result.Should().BeNull("Global Query Filter deve impedir acesso cross-tenant (ADR-0001)");
    }

    [Fact(DisplayName = "IssueTokenAsync com hash duplicado lança DbUpdateException (UNIQUE token_hash)")]
    public async Task IssueTokenAsync_DuplicateHash_ThrowsDbUpdateException()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        var actionToken = await CreateTokenFactory()
            .IssueAsync(tenantId, userId, activityId, ActionType.Complete);

        // Duas entidades com o mesmo token_hash (mesmo ActionToken reutilizado — cenário de bug)
        var entity1 = DigestActionToken.Issue(tenantId, userId, activityId, ActionType.Complete, actionToken);
        var entity2 = DigestActionToken.Issue(tenantId, userId, Guid.NewGuid(), ActionType.Complete, actionToken);

        await using var ctx1 = _fixture.CreateDbContext(tenantId);
        var repo1 = new DigestActionTokenRepository(ctx1);
        await repo1.IssueTokenAsync(entity1);

        await using var ctx2 = _fixture.CreateDbContext(tenantId);
        var repo2 = new DigestActionTokenRepository(ctx2);

        var act = async () => await repo2.IssueTokenAsync(entity2);

        // UNIQUE constraint em token_hash deve rejeitar duplicata (DD-007)
        await act.Should().ThrowAsync<DbUpdateException>(
            "UNIQUE constraint uq_digest_action_tokens_hash impede hash duplicado (DD-007)");
    }
}
