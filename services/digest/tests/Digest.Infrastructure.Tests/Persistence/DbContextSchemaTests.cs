using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Xunit;

namespace Digest.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração para <c>DigestDbContext</c>: schema, UNIQUE constraints, índices e Global Query Filter.
/// Usa Testcontainers com PostgreSQL real (TASK-14).
/// </summary>
[Collection("Postgres")]
public sealed class DbContextSchemaTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public DbContextSchemaTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Migrations criam tabela email_digest_logs com índice UNIQUE e índices compostos")]
    public async Task EmailDigestLogs_TableExists_WithUniqueIndex()
    {
        // Arrange
        await using var ctx = _fixture.CreateDbContextWithoutTenant();

        // Assert via pg_indexes — o EF Core cria UNIQUE como índice
        var indexes = await ctx.Database.SqlQueryRaw<string>(
            @"SELECT indexname FROM pg_indexes WHERE tablename = 'email_digest_logs'").ToListAsync();

        indexes.Should().Contain(i => i.Contains("tenant_user_date"),
            "UNIQUE (tenant_id, user_id, digest_date) deve existir (RN-010)");
        indexes.Should().Contain(i => i.Contains("tenant_date"),
            "índice de acesso por tenant + data deve existir");
        indexes.Should().Contain(i => i.Contains("status"),
            "índice por status deve existir");
    }

    [Fact(DisplayName = "Migrations criam tabela digest_action_tokens com índice UNIQUE em token_hash")]
    public async Task DigestActionTokens_TableExists_WithUniqueOnTokenHash()
    {
        // Arrange
        await using var ctx = _fixture.CreateDbContextWithoutTenant();

        // Assert via pg_indexes
        var indexes = await ctx.Database.SqlQueryRaw<string>(
            @"SELECT indexname FROM pg_indexes WHERE tablename = 'digest_action_tokens'").ToListAsync();

        indexes.Should().Contain(i => i.Contains("hash"),
            "UNIQUE token_hash deve existir (DD-007, anti-enumeração)");
        indexes.Should().Contain(i => i.Contains("expires"),
            "índice de expiração deve existir para purge (RNF 9.2)");
    }

    [Fact(DisplayName = "Migrations criam tabela outbox_messages")]
    public async Task OutboxMessages_TableExists()
    {
        // Arrange
        await using var ctx = _fixture.CreateDbContextWithoutTenant();

        // Act/Assert — verifica via índice
        var indexes = await ctx.Database.SqlQueryRaw<string>(
            @"SELECT indexname FROM pg_indexes WHERE tablename = 'outbox_messages'").ToListAsync();

        indexes.Should().Contain(i => i.Contains("pending"));
    }

    [Fact(DisplayName = "Global Query Filter filtra por tenant_id corretamente")]
    public async Task GlobalQueryFilter_FiltersByTenantId()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var digestDate = new DigestDate(new LocalDate(2026, 6, 14));

        // Insere registro para tenant A diretamente (sem filtro)
        await using (var adminCtx = _fixture.CreateDbContextWithoutTenant())
        {
            await adminCtx.Database.ExecuteSqlAsync(
                $"SET app.current_tenant = '{tenantA}'");
            var log = EmailDigestLog.Schedule(tenantA, userId, digestDate, null);
            adminCtx.EmailDigestLogs.Add(log);
            await adminCtx.SaveChangesAsync();
        }

        // Act — lê com contexto de tenant B (deve retornar vazio)
        await using var ctxB = _fixture.CreateDbContext(tenantB);
        await ctxB.Database.ExecuteSqlAsync($"SET app.current_tenant = '{tenantB}'");
        var logsB = await ctxB.EmailDigestLogs.ToListAsync();

        // Assert
        logsB.Should().BeEmpty("Global Query Filter deve impedir acesso cross-tenant");

        // Act — lê com contexto de tenant A (deve retornar o registro)
        await using var ctxA = _fixture.CreateDbContext(tenantA);
        await ctxA.Database.ExecuteSqlAsync($"SET app.current_tenant = '{tenantA}'");
        var logsA = await ctxA.EmailDigestLogs.ToListAsync();

        logsA.Should().HaveCount(1);
    }

    [Fact(DisplayName = "Sem DbSet de tabelas de outros BCs (Req 6.2)")]
    public void DbContext_HasNo_ForeignBcDbSets()
    {
        // Arrange
        using var ctx = _fixture.CreateDbContextWithoutTenant();
        var entityTypeNames = ctx.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t != null)
            .ToList();

        // Assert — proíbe mapeamento de tabelas de outros BCs
        entityTypeNames.Should().NotContain("opportunities");
        entityTypeNames.Should().NotContain("activities");
        entityTypeNames.Should().NotContain("goals");
        entityTypeNames.Should().NotContain("users");
    }
}

/// <summary>Coleção xUnit para compartilhar o container PostgreSQL entre os testes.</summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;
