using Authentication.Infrastructure.Directory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Authentication.Infrastructure.Tests.Directory;

/// <summary>
/// Testes de integração de <see cref="TenantDirectory"/> e <see cref="OrganizationUserDirectory"/>
/// contra PostgreSQL real via Testcontainers.
///
/// DÍVIDA TÉCNICA: O schema criado aqui é local ao módulo authentication.
/// Em produção, as tabelas são de propriedade de tenant-administration e organization.
/// Este schema de teste representa a estrutura esperada dos módulos donos.
///
/// Mapeia: Req 1.3, Req 5.1; design.md § 6.1, § 7; DD-001, TASK-14.
/// </summary>
[Collection("PostgreSQL")]
public sealed class DirectoryAdapterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("auth_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private DirectoryDbContext _context = null!;
    private TenantDirectory _tenantDirectory = null!;
    private OrganizationUserDirectory _userDirectory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new DirectoryDbContext(options);

        // Cria o schema de teste (dívida técnica: produção usa schema dos módulos donos)
        await _context.Database.EnsureCreatedAsync();

        _tenantDirectory = new TenantDirectory(_context);
        _userDirectory = new OrganizationUserDirectory(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // =========================================================================
    // TenantDirectory — slug ativo retorna tenant_id e identity_tenant_id
    // =========================================================================

    [Fact(DisplayName = "TenantDirectory — slug ativo retorna tenant_id e identity_tenant_id corretos (TASK-14, Req 1.3)")]
    public async Task ResolveSlugAsync_ActiveSlug_ReturnsTenantData()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId, slug: "acme", identityTenantId: "firebase-tenant-acme", status: "active");

        // Act
        var result = await _tenantDirectory.ResolveSlugAsync("acme");

        // Assert
        result.Should().NotBeNull(because: "slug ativo deve retornar resultado não-nulo");
        result!.TenantId.Should().Be(tenantId);
        result.IdentityTenantId.Should().Be("firebase-tenant-acme");
    }

    // =========================================================================
    // TenantDirectory — slug inexistente retorna null
    // =========================================================================

    [Fact(DisplayName = "TenantDirectory — slug inexistente retorna null (TASK-14, AUTH-ERR-010)")]
    public async Task ResolveSlugAsync_UnknownSlug_ReturnsNull()
    {
        // Arrange — não insere nenhum tenant

        // Act
        var result = await _tenantDirectory.ResolveSlugAsync("unknown-slug");

        // Assert
        result.Should().BeNull(
            because: "slug inexistente deve retornar null — leva a 404 AUTH-ERR-010 (design.md § 12)");
    }

    [Fact(DisplayName = "TenantDirectory — slug de tenant inativo retorna null (TASK-14)")]
    public async Task ResolveSlugAsync_InactiveTenant_ReturnsNull()
    {
        // Arrange
        await SeedTenantAsync(Guid.NewGuid(), slug: "inactive-tenant", identityTenantId: "fi-inactive", status: "inactive");

        // Act
        var result = await _tenantDirectory.ResolveSlugAsync("inactive-tenant");

        // Assert
        result.Should().BeNull(
            because: "tenant inativo deve ser tratado como não encontrado (Req 1)");
    }

    // =========================================================================
    // TenantDirectory — normalização de slug
    // =========================================================================

    [Fact(DisplayName = "TenantDirectory — slug com maiúsculas é normalizado (TASK-14)")]
    public async Task ResolveSlugAsync_SlugWithUpperCase_NormalizesAndFinds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId, slug: "globo", identityTenantId: "fi-globo", status: "active");

        // Act
        var result = await _tenantDirectory.ResolveSlugAsync("GLOBO");

        // Assert
        result.Should().NotBeNull(because: "slug deve ser normalizado para lowercase antes da busca");
        result!.TenantId.Should().Be(tenantId);
    }

    // =========================================================================
    // OrganizationUserDirectory — identity_uid válido retorna user_id e memberships
    // =========================================================================

    [Fact(DisplayName = "OrganizationUserDirectory — providerRef válido retorna user_id e memberships (TASK-14, Req 5.1)")]
    public async Task FindUserAsync_ValidProviderRef_ReturnsUserAndMemberships()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var buId = Guid.NewGuid();
        await SeedUserAsync(userId, tenantId, providerRef: "firebase-uid-abc", email: "user@test.com", status: "active");
        await SeedMembershipAsync(userId, tenantId, buId, role: "admin");

        // Act
        var result = await _userDirectory.FindUserAsync("firebase-uid-abc", tenantId);

        // Assert
        result.Should().NotBeNull(because: "providerRef válido deve retornar usuário");
        result!.UserId.Should().Be(userId,
            because: "OrganizationUserDirectory nunca expõe identity_uid — retorna user_id interno (DD-001)");
        result.Email.Should().Be("user@test.com");
        result.IsActive.Should().BeTrue();
        result.Memberships.Entries.Should().HaveCount(1,
            because: "membership do usuário deve ser carregado");
        result.Memberships.Entries[0].BuId.Should().Be(buId);
        result.Memberships.Entries[0].Role.Should().Be("admin");
    }

    // =========================================================================
    // OrganizationUserDirectory — identity_uid inválido retorna null
    // =========================================================================

    [Fact(DisplayName = "OrganizationUserDirectory — providerRef inexistente retorna null (TASK-14)")]
    public async Task FindUserAsync_UnknownProviderRef_ReturnsNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _userDirectory.FindUserAsync("unknown-uid", tenantId);

        // Assert
        result.Should().BeNull(
            because: "providerRef inexistente deve retornar null (leva a 403 AUTH-ERR-005)");
    }

    // =========================================================================
    // Schema assertion — nenhuma coluna de senha em users (RNF 1.1)
    // =========================================================================

    [Fact(DisplayName = "Schema — tabela users não possui coluna de senha (RNF 1.1)")]
    public async Task Schema_UsersTable_DoesNotHavePasswordColumn()
    {
        // Arrange — verifica via SQL que não existe coluna com nome relacionado a senha
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'users'
              AND (lower(column_name) LIKE '%password%'
                OR lower(column_name) LIKE '%senha%'
                OR lower(column_name) LIKE '%hash%'
                OR lower(column_name) LIKE '%pwd%')";

        using var reader = await cmd.ExecuteReaderAsync();
        var passwordColumns = new List<string>();
        while (await reader.ReadAsync())
            passwordColumns.Add(reader.GetString(0));

        // Assert
        passwordColumns.Should().BeEmpty(
            because: "RNF 1.1: nenhuma coluna de senha deve existir em users — " +
                     "autenticação é delegada inteiramente ao Identity Platform (DEC-005)");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task SeedTenantAsync(
        Guid id, string slug, string identityTenantId, string status)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO tenants (id, slug, identity_tenant_id, status) VALUES ({0}, {1}, {2}, {3})",
            id, slug, identityTenantId, status);
    }

    private async Task SeedUserAsync(
        Guid id, Guid tenantId, string providerRef, string email, string status)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO users (id, tenant_id, provider_user_ref, email, auth_method, status) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
            id, tenantId, providerRef, email, "password", status);
    }

    private async Task SeedMembershipAsync(
        Guid userId, Guid tenantId, Guid buId, string role)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO user_memberships (id, tenant_id, user_id, bu_id, role) " +
            "VALUES ({0}, {1}, {2}, {3}, {4})",
            Guid.NewGuid(), tenantId, userId, buId, role);
    }
}
