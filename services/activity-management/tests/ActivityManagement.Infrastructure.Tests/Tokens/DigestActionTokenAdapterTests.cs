namespace ActivityManagement.Infrastructure.Tests.Tokens;

using System.Security.Cryptography;
using System.Text;
using ActivityManagement.Infrastructure.Persistence;
using ActivityManagement.Infrastructure.Tokens;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Testes de integração para <see cref="DigestActionTokenAdapter"/> com PostgreSQL real.
///
/// Schema canônico (ADR-0006, alinhado ao digest):
///   - token_hash: BYTEA NOT NULL (SHA-256, 32 bytes).
///   - activity_id: UUID NOT NULL (referência lógica sem FK física — DD-001).
///   - action: VARCHAR(20) com casing 'Complete'/'Reschedule'.
///
/// Garantias testadas:
///   1. Token válido (não expirado, não usado) → retorna dados corretos.
///   2. Token expirado → retorna dados (decisão de rejeitar cabe ao domínio).
///   3. Token já usado (used_at preenchido) → retorna dados com used_at.
///   4. Token inexistente → retorna nulo.
///   5. MarkUsedAsync preenche used_at somente quando nulo (idempotência).
///   6. MarkUsedAsync é no-op se used_at já preenchido (uso-único garantido).
///   7. FindByHashAsync retorna nulo quando tenant não coincide (Global Query Filter).
///
/// Mapeia: TASK-17, DD-003, DD-007, ADR-0006, RNF 5, design §6.4, Req 7.
/// </summary>
public sealed class DigestActionTokenAdapterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .WithDatabase("token_adapter_test")
        .Build();

    private NpgsqlConnection _conn = null!;
    private ActivityManagementDbContext _ctx = null!;
    private DigestActionTokenAdapter _adapter = null!;

    private static readonly Guid TenantId   = Guid.NewGuid();
    private static readonly Guid UserId     = Guid.NewGuid();
    private static readonly Guid ActivityId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await _conn.OpenAsync();

        await SetupSchemaAsync();

        var options = new DbContextOptionsBuilder<ActivityManagementDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _ctx = new ActivityManagementDbContext(options);
        _ctx.SetTenant(TenantId);

        _adapter = new DigestActionTokenAdapter(_ctx);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _conn.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computa SHA-256 de um token em claro — idêntico ao algoritmo do digest (ADR-0006).
    /// SHA256(UTF-8(clearToken)) → 32 bytes.
    /// </summary>
    private static byte[] ComputeHash(string clearToken)
        => SHA256.HashData(Encoding.UTF8.GetBytes(clearToken));

    private async Task SetupSchemaAsync()
    {
        await using var cmd = new NpgsqlCommand(SchemaSetupSql, _conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Insere um token com token_hash BYTEA e activity_id NOT NULL — schema canônico do digest (ADR-0006).
    /// </summary>
    private async Task InsertTokenAsync(
        byte[]          tokenHash,
        DateTimeOffset  expiresAt,
        DateTimeOffset? usedAt = null,
        Guid?           overrideTenant = null)
    {
        var tenant = overrideTenant ?? TenantId;
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO digest_action_tokens
                (id, tenant_id, user_id, activity_id, action, token_hash, expires_at, used_at)
            VALUES
                (gen_random_uuid(), @tenantId, @userId, @activityId, 'Complete', @hash, @expiresAt, @usedAt)", _conn);
        cmd.Parameters.AddWithValue("tenantId",   tenant);
        cmd.Parameters.AddWithValue("userId",     UserId);
        cmd.Parameters.AddWithValue("activityId", ActivityId);
        // BYTEA: Npgsql representa byte[] como NpgsqlDbType.Bytea automaticamente
        cmd.Parameters.Add(new NpgsqlParameter("hash", NpgsqlTypes.NpgsqlDbType.Bytea) { Value = tokenHash });
        cmd.Parameters.AddWithValue("expiresAt",  expiresAt);
        cmd.Parameters.AddWithValue("usedAt",     (object?)usedAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Testes — FindByHashAsync ───────────────────────────────────────────────

    [Fact]
    public async Task FindByHashAsync_ValidToken_Returns_TokenData()
    {
        // Arrange: token em claro → SHA-256 → BYTEA (fluxo real do digest)
        const string clearToken = "valid-clear-token-abc123";
        var hash      = ComputeHash(clearToken);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);
        await InsertTokenAsync(hash, expiresAt);

        // Act: adapter busca por byte[]
        var result = await _adapter.FindByHashAsync(hash);

        // Assert
        result.Should().NotBeNull(because: "token válido deve ser encontrado pelo hash BYTEA");
        result!.TenantId.Should().Be(TenantId);
        result.UserId.Should().Be(UserId);
        result.ActivityId.Should().Be(ActivityId, because: "activity_id é NOT NULL (ADR-0006)");
        result.Action.Should().Be("Complete");
        result.UsedAt.Should().BeNull(because: "token ainda não foi usado");
    }

    [Fact]
    public async Task FindByHashAsync_NonExistentToken_Returns_Null()
    {
        // Act: hash de zeros — nunca emitido
        var result = await _adapter.FindByHashAsync(new byte[32]);

        // Assert: anti-enumeração — retorna nulo sem distinguir inexistente de inacessível
        result.Should().BeNull(because: "token inexistente deve retornar nulo (anti-enumeração, Req 7.6)");
    }

    [Fact]
    public async Task FindByHashAsync_ExpiredToken_Returns_TokenData_With_Expired_ExpiresAt()
    {
        // Arrange: token expirado
        const string clearToken = "expired-token-xyz";
        var hash      = ComputeHash(clearToken);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(-1); // expirado
        await InsertTokenAsync(hash, expiresAt);

        // Act
        var result = await _adapter.FindByHashAsync(hash);

        // Assert: retorna o dado (a decisão de rejeitar token expirado cabe ao domain/application)
        result.Should().NotBeNull(because: "adapter retorna o registro; camada superior decide se expirado = inválido");
        result!.ExpiresAt.Should().BeBefore(DateTimeOffset.UtcNow,
            because: "o token deve estar expirado");
    }

    [Fact]
    public async Task FindByHashAsync_UsedToken_Returns_TokenData_With_UsedAt_Set()
    {
        // Arrange: token já usado
        const string clearToken = "used-token-def456";
        var hash   = ComputeHash(clearToken);
        var usedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await InsertTokenAsync(hash, DateTimeOffset.UtcNow.AddHours(24), usedAt);

        // Act
        var result = await _adapter.FindByHashAsync(hash);

        // Assert: retorna dados incluindo used_at (camada superior decide sobre uso-único)
        result.Should().NotBeNull();
        result!.UsedAt.Should().NotBeNull(because: "token já consumido deve ter used_at preenchido");
    }

    [Fact]
    public async Task FindByHashAsync_WrongTenant_Returns_Null()
    {
        // Arrange: token de outro tenant
        const string clearToken = "other-tenant-token";
        var hash        = ComputeHash(clearToken);
        var otherTenant = Guid.NewGuid();
        await InsertTokenAsync(hash, DateTimeOffset.UtcNow.AddHours(24), overrideTenant: otherTenant);

        // Act: busca no contexto do TenantId corrente (Global Query Filter ativo)
        var result = await _adapter.FindByHashAsync(hash);

        // Assert: Global Query Filter impede acesso a tokens de outro tenant
        result.Should().BeNull(
            because: "token de outro tenant não deve ser acessível (Global Query Filter, ADR-0001)");
    }

    // ── Testes — MarkUsedAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task MarkUsedAsync_Sets_UsedAt_When_Null()
    {
        // Arrange: token válido, ainda não usado
        const string clearToken = "mark-used-token";
        var hash      = ComputeHash(clearToken);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);
        await InsertTokenAsync(hash, expiresAt);

        // Recuperar o token para obter o Id
        var tokenData = await _adapter.FindByHashAsync(hash);
        tokenData.Should().NotBeNull();

        var usedAt = DateTimeOffset.UtcNow;

        // Act
        await _adapter.MarkUsedAsync(tokenData!.Id, usedAt);
        await _ctx.SaveChangesAsync();

        // Assert: used_at deve estar preenchido no banco
        await using var checkCmd = new NpgsqlCommand(
            "SELECT used_at FROM digest_action_tokens WHERE token_hash = @hash", _conn);
        checkCmd.Parameters.Add(new NpgsqlParameter("hash", NpgsqlTypes.NpgsqlDbType.Bytea) { Value = hash });
        var dbUsedAt = await checkCmd.ExecuteScalarAsync();
        dbUsedAt.Should().NotBe(DBNull.Value, because: "MarkUsedAsync deve preencher used_at");
    }

    [Fact]
    public async Task MarkUsedAsync_Is_Noop_When_AlreadyUsed()
    {
        // Arrange: token já usado
        const string clearToken = "already-used-token";
        var hash           = ComputeHash(clearToken);
        var originalUsedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        await InsertTokenAsync(hash, DateTimeOffset.UtcNow.AddHours(24), originalUsedAt);

        var tokenData = await _adapter.FindByHashAsync(hash);
        tokenData.Should().NotBeNull();

        // Act: chamar MarkUsed com um instante diferente
        await _adapter.MarkUsedAsync(tokenData!.Id, DateTimeOffset.UtcNow);
        await _ctx.SaveChangesAsync();

        // Assert: used_at original deve ser mantido (UPDATE WHERE used_at IS NULL)
        DateTimeOffset? dbUsedAt = null;
        await using var checkCmd = new NpgsqlCommand(
            "SELECT used_at AT TIME ZONE 'UTC' FROM digest_action_tokens WHERE token_hash = @hash", _conn);
        checkCmd.Parameters.Add(new NpgsqlParameter("hash", NpgsqlTypes.NpgsqlDbType.Bytea) { Value = hash });
        await using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync() && !reader.IsDBNull(0))
        {
            // Npgsql retorna DateTime (UTC) para timestamptz — converte para DateTimeOffset
            var dt = reader.GetDateTime(0);
            dbUsedAt = new DateTimeOffset(dt, TimeSpan.Zero);
        }

        // Deve permanecer próximo ao original (tolerância de 1 segundo)
        dbUsedAt.Should().NotBeNull();
        dbUsedAt!.Value.Should().BeCloseTo(originalUsedAt, TimeSpan.FromSeconds(1),
            because: "MarkUsedAsync não deve sobrescrever used_at já preenchido (uso único, RNF 5.2)");
    }

    // ── Schema canônico (ADR-0006, alinhado ao digest) ─────────────────────────

    private const string SchemaSetupSql = @"
        CREATE TABLE IF NOT EXISTS digest_action_tokens (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            user_id         UUID NOT NULL,
            activity_id     UUID NOT NULL,
            action          VARCHAR(20) NOT NULL DEFAULT 'Complete',
            token_hash      BYTEA NOT NULL,
            expires_at      TIMESTAMPTZ NOT NULL,
            used_at         TIMESTAMPTZ,
            created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
            CONSTRAINT uq_digest_action_tokens_hash UNIQUE (token_hash),
            CONSTRAINT ck_digest_action_tokens_action CHECK (action IN ('Complete','Reschedule'))
        );

        CREATE INDEX IF NOT EXISTS ix_digest_action_tokens_expires
            ON digest_action_tokens (expires_at);
    ";
}
