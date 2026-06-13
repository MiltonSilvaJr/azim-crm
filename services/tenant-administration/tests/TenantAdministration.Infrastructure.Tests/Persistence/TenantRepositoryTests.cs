using FluentAssertions;
using Npgsql;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.ValueObjects;
using TenantAdministration.Infrastructure.Tests.Fixtures;
using Xunit;

namespace TenantAdministration.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração do <see cref="TenantRepository"/> com PostgreSQL real (Testcontainers).
/// Cobre TASK-12: mapeamento EF Core, constraints, trigger de slug imutável.
/// </summary>
[Collection("Postgres")]
public sealed class TenantRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public TenantRepositoryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "AddAsync persiste tenant e FindByIdAsync recupera o agregado")]
    public async Task AddAsync_PersistsAndRetrievesTenant()
    {
        // Arrange
        var (db, repo, _) = _fixture.CreateRepositoryContext();
        var slug = Slug.Create("test-tenant-repo").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var dt = DigestTime.Create("07:00").Value;
        var tenant = Tenant.Provision(slug, "Test Tenant", tz, dt, "admin@test.com", DateTimeOffset.UtcNow);

        // Act
        await repo.AddAsync(tenant);
        await db.SaveChangesAsync();

        // Assert
        using var readDb = _fixture.CreateDbContext();
        var found = await new Infrastructure.Persistence.Repositories.TenantRepository(readDb)
            .FindByIdAsync(tenant.Id);
        found.Should().NotBeNull();
        found!.Slug.Value.Should().Be("test-tenant-repo");
        found.DisplayName.Should().Be("Test Tenant");
        found.Status.Should().Be(TenantStatus.Provisioned);
    }

    [Fact(DisplayName = "ExistsSlugAsync retorna true para slug já existente")]
    public async Task ExistsSlugAsync_ReturnsTrue_WhenSlugExists()
    {
        // Arrange
        var (db, repo, _) = _fixture.CreateRepositoryContext();
        var slug = Slug.Create("slug-exists-check").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var dt = DigestTime.Create("07:00").Value;
        var tenant = Tenant.Provision(slug, "Exists Check", tz, dt, "admin@ec.com", DateTimeOffset.UtcNow);
        await repo.AddAsync(tenant);
        await db.SaveChangesAsync();

        // Act
        var exists = await repo.ExistsSlugAsync("slug-exists-check");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact(DisplayName = "ExistsSlugAsync retorna false para slug inexistente")]
    public async Task ExistsSlugAsync_ReturnsFalse_WhenSlugNotExists()
    {
        // Arrange
        var (_, repo, _) = _fixture.CreateRepositoryContext();

        // Act
        var exists = await repo.ExistsSlugAsync("absolutely-no-such-slug-xyzzy");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact(DisplayName = "FindBySlugAsync retorna tenant pelo slug")]
    public async Task FindBySlugAsync_ReturnsTenant()
    {
        // Arrange
        var (db, repo, _) = _fixture.CreateRepositoryContext();
        var slug = Slug.Create("find-by-slug-test").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var dt = DigestTime.Create("07:00").Value;
        var tenant = Tenant.Provision(slug, "Find By Slug", tz, dt, "admin@fbs.com", DateTimeOffset.UtcNow);
        await repo.AddAsync(tenant);
        await db.SaveChangesAsync();

        // Act
        var found = await repo.FindBySlugAsync("find-by-slug-test");

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(tenant.Id);
    }

    [Fact(DisplayName = "Trigger prevent_slug_update impede UPDATE da coluna slug via SQL direto")]
    public async Task PreventSlugUpdate_Trigger_BlocksSlugChange()
    {
        // Arrange — inserir tenant diretamente para garantir que existe
        var tenantId = Guid.NewGuid();
        using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = $"""
            INSERT INTO tenants (id, slug, display_name, status, active, iana_timezone, digest_time, provisioned_at, created_at, updated_at)
            VALUES ('{tenantId}', 'trigger-test-slug', 'Trigger Test', 'provisioned', true, 'America/Sao_Paulo', '07:00', now(), now(), now())
            """;
        await insertCmd.ExecuteNonQueryAsync();

        // Act + Assert — tentar mudar o slug deve lançar exceção
        await using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = $"UPDATE tenants SET slug = 'changed-slug' WHERE id = '{tenantId}'";
        var act = async () => await updateCmd.ExecuteNonQueryAsync();

        await act.Should().ThrowAsync<PostgresException>()
            .WithMessage("*TA-ERR-SLUG-IMMUTABLE*");
    }

    [Fact(DisplayName = "UpdateAsync persiste mudanças de estado no tenant")]
    public async Task UpdateAsync_PersistsStatusChange()
    {
        // Arrange
        var (db, repo, _) = _fixture.CreateRepositoryContext();
        var slug = Slug.Create("update-status-test").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        var dt = DigestTime.Create("07:00").Value;
        var tenant = Tenant.Provision(slug, "Update Test", tz, dt, "admin@up.com", DateTimeOffset.UtcNow);
        await repo.AddAsync(tenant);
        await db.SaveChangesAsync();

        // Act
        tenant.Suspend(DateTimeOffset.UtcNow);
        await repo.UpdateAsync(tenant);
        await db.SaveChangesAsync();

        // Assert
        using var readDb = _fixture.CreateDbContext();
        var updated = await new Infrastructure.Persistence.Repositories.TenantRepository(readDb)
            .FindByIdAsync(tenant.Id);
        updated!.Status.Should().Be(TenantStatus.Suspended);
    }
}

/// <summary>
/// Coleção xUnit para agrupar testes que compartilham o container PostgreSQL.
/// </summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture> { }
