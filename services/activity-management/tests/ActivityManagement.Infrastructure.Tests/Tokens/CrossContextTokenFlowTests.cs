namespace ActivityManagement.Infrastructure.Tests.Tokens;

using System.Security.Cryptography;
using System.Text;
using ActivityManagement.Infrastructure.Persistence;
using ActivityManagement.Infrastructure.Tokens;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;
using Xunit;

/// <summary>
/// Teste de integração cross-context: prova o fluxo emissão (digest) → consumo (activity-management)
/// no MESMO banco compartilhado (ADR-0006).
///
/// O banco compartilhado (azim_shared) é simulado pelo Testcontainer.
/// A emissão é feita por INSERT direto com os mesmos parâmetros que o digest usaria:
///   - token_hash: SHA256(UTF-8(clearToken)) como BYTEA (32 bytes).
///   - activity_id: NOT NULL, sem FK física (DD-001).
///   - action: 'Complete' ou 'Reschedule' (casing do enum ActionType do digest).
///   - expires_at: created_at + TTL (simulado com 48h de TTL).
///   - RLS: NULLIF(current_setting('app.current_tenant', true), '')::UUID (ADR-0001).
///
/// Garantias testadas:
///   1. Fluxo emissão→consumo ponta a ponta: hash do clear token leva ao registro correto.
///   2. MarkUsedAsync grava used_at na mesma transação (atomicidade, Req 7.3).
///   3. Tenant isolation: token de TenantA não é acessível por TenantB (RLS + Global Query Filter).
///   4. Uso único: segundo consumo é no-op idempotente (UPDATE WHERE used_at IS NULL).
///
/// Mapeia: ADR-0006, DD-003, DD-007, ADR-0001, Req 7.3, RNF 5, design §6.4.
/// </summary>
public sealed class CrossContextTokenFlowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithUsername("rootuser")
        .WithPassword("rootpass")
        .WithDatabase("azim_shared_test")
        .Build();

    private NpgsqlConnection _adminConn = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _adminConn = new NpgsqlConnection(_postgres.GetConnectionString());
        await _adminConn.OpenAsync();

        // Schema canônico do digest — simula as migrations do Digest.Infrastructure rodando primeiro
        await ExecuteAdminAsync(SharedSchemaSetupSql);
    }

    public async Task DisposeAsync()
    {
        await _adminConn.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, _adminConn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Computa SHA-256 do token em claro — idêntico ao digest (ActionToken.ComputeHash / ADR-0006).
    /// SHA256(UTF-8(clearToken)) → 32 bytes.
    /// </summary>
    private static byte[] ComputeHash(string clearToken)
        => SHA256.HashData(Encoding.UTF8.GetBytes(clearToken));

    /// <summary>
    /// Simula a emissão do token pelo digest (BC-06):
    /// INSERT na digest_action_tokens com token_hash BYTEA e activity_id NOT NULL.
    /// </summary>
    private async Task<Guid> EmitTokenAsDigestAsync(
        string         clearToken,
        Guid           tenantId,
        Guid           userId,
        Guid           activityId,
        string         action,
        DateTimeOffset expiresAt)
    {
        var tokenHash = ComputeHash(clearToken);
        var id = Guid.NewGuid();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO digest_action_tokens
                (id, tenant_id, user_id, activity_id, action, token_hash, expires_at, created_at)
            VALUES
                (@id, @tenantId, @userId, @activityId, @action, @hash, @expiresAt, now())",
            _adminConn);

        cmd.Parameters.AddWithValue("id",         id);
        cmd.Parameters.AddWithValue("tenantId",   tenantId);
        cmd.Parameters.AddWithValue("userId",     userId);
        cmd.Parameters.AddWithValue("activityId", activityId);
        cmd.Parameters.AddWithValue("action",     action);
        cmd.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = tokenHash });
        cmd.Parameters.AddWithValue("expiresAt",  expiresAt);

        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private ActivityManagementDbContext CreateConsumerContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ActivityManagementDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var ctx = new ActivityManagementDbContext(options);
        ctx.SetTenant(tenantId);
        return ctx;
    }

    private async Task<DateTimeOffset?> ReadUsedAtAsync(byte[] tokenHash)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT used_at AT TIME ZONE 'UTC' FROM digest_action_tokens WHERE token_hash = @hash",
            _adminConn);
        cmd.Parameters.Add(new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = tokenHash });
        var result = await cmd.ExecuteScalarAsync();
        if (result is DBNull || result is null)
            return null;
        // Npgsql retorna DateTime (UTC) para timestamptz — converte para DateTimeOffset
        var dt = (DateTime)result;
        return new DateTimeOffset(dt, TimeSpan.Zero);
    }

    // ── Testes cross-context ───────────────────────────────────────────────────

    [Fact]
    public async Task CrossContext_EmitByDigest_ConsumeByActivityManagement_FindsToken()
    {
        // Arrange: emissão pelo digest com SHA-256 BYTEA
        var tenantId   = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        const string clearToken = "end-to-end-clear-token-complete";
        var expiresAt  = DateTimeOffset.UtcNow.AddHours(48);

        await EmitTokenAsDigestAsync(clearToken, tenantId, userId, activityId, "Complete", expiresAt);

        // Act: consumo pelo activity-management — computa o mesmo SHA-256
        var tokenHash = ComputeHash(clearToken);
        await using var ctx = CreateConsumerContext(tenantId);
        var adapter = new DigestActionTokenAdapter(ctx);

        var result = await adapter.FindByHashAsync(tokenHash);

        // Assert: o mesmo token emitido pelo digest é encontrado pelo activity-management
        result.Should().NotBeNull(
            because: "o hash SHA-256 do clear token deve coincidir com o BYTEA persistido pelo digest");
        result!.TenantId.Should().Be(tenantId);
        result.ActivityId.Should().Be(activityId, because: "activity_id é NOT NULL (ADR-0006, DD-001)");
        result.Action.Should().Be("Complete");
        result.UsedAt.Should().BeNull(because: "token ainda não foi consumido");
        result.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CrossContext_MarkUsed_PersistsUsedAt_EndToEnd()
    {
        // Arrange
        var tenantId   = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        const string clearToken = "end-to-end-mark-used-token";
        var expiresAt  = DateTimeOffset.UtcNow.AddHours(48);

        await EmitTokenAsDigestAsync(clearToken, tenantId, userId, activityId, "Complete", expiresAt);

        var tokenHash = ComputeHash(clearToken);
        await using var ctx = CreateConsumerContext(tenantId);
        var adapter = new DigestActionTokenAdapter(ctx);

        var found = await adapter.FindByHashAsync(tokenHash);
        found.Should().NotBeNull();

        var usedAt = DateTimeOffset.UtcNow;

        // Act: marca como usado (mesmo comportamento do ProcessDigestActionCommand)
        await adapter.MarkUsedAsync(found!.Id, usedAt);
        await ctx.SaveChangesAsync();

        // Assert: used_at gravado no banco compartilhado
        var dbUsedAt = await ReadUsedAtAsync(tokenHash);
        dbUsedAt.Should().NotBeNull(because: "MarkUsedAsync deve persistir used_at no banco compartilhado");
        dbUsedAt!.Value.Should().BeCloseTo(usedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CrossContext_TenantIsolation_TenantA_CannotSee_TenantB_Token()
    {
        // Arrange: emit token do TenantA
        var tenantA    = Guid.NewGuid();
        var tenantB    = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        const string clearToken = "tenant-isolation-token";
        var expiresAt  = DateTimeOffset.UtcNow.AddHours(48);

        await EmitTokenAsDigestAsync(clearToken, tenantA, Guid.NewGuid(), activityId, "Complete", expiresAt);

        var tokenHash = ComputeHash(clearToken);

        // Act: context do TenantB tenta buscar o token
        await using var ctxB = CreateConsumerContext(tenantB);
        var adapterB = new DigestActionTokenAdapter(ctxB);

        var result = await adapterB.FindByHashAsync(tokenHash);

        // Assert: Global Query Filter impede cross-tenant (ADR-0001)
        result.Should().BeNull(
            because: "token do TenantA não deve ser acessível pelo TenantB (ADR-0001, Global Query Filter)");
    }

    [Fact]
    public async Task CrossContext_SingleUse_SecondConsume_IsNoop_Idempotent()
    {
        // Arrange: emite e consome o token uma vez
        var tenantId   = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        const string clearToken = "single-use-idempotent-token";
        var expiresAt  = DateTimeOffset.UtcNow.AddHours(48);

        await EmitTokenAsDigestAsync(clearToken, tenantId, Guid.NewGuid(), activityId, "Complete", expiresAt);

        var tokenHash  = ComputeHash(clearToken);
        var firstUsedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Primeiro consumo
        await using var ctx1 = CreateConsumerContext(tenantId);
        var adapter1 = new DigestActionTokenAdapter(ctx1);
        var found1 = await adapter1.FindByHashAsync(tokenHash);
        found1.Should().NotBeNull();
        await adapter1.MarkUsedAsync(found1!.Id, firstUsedAt);
        await ctx1.SaveChangesAsync();

        // Act: segundo consumo com instante diferente (idempotência)
        var secondUsedAt = DateTimeOffset.UtcNow;
        await using var ctx2 = CreateConsumerContext(tenantId);
        var adapter2 = new DigestActionTokenAdapter(ctx2);
        var found2 = await adapter2.FindByHashAsync(tokenHash);
        found2.Should().NotBeNull(because: "token ainda é encontrável mesmo após consumo");
        found2!.UsedAt.Should().NotBeNull(because: "already consumed");

        await adapter2.MarkUsedAsync(found2.Id, secondUsedAt);
        await ctx2.SaveChangesAsync();

        // Assert: used_at original deve ser preservado (UPDATE WHERE used_at IS NULL = no-op)
        var dbUsedAt = await ReadUsedAtAsync(tokenHash);
        dbUsedAt.Should().NotBeNull();
        dbUsedAt!.Value.Should().BeCloseTo(firstUsedAt, TimeSpan.FromSeconds(1),
            because: "segundo consumo não deve sobrescrever used_at (uso único, RNF 5.2, ADR-0006)");
    }

    [Fact]
    public async Task CrossContext_RescheduleToken_IsFound_ByConsumer()
    {
        // Arrange: emite token de Reschedule
        var tenantId   = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        const string clearToken = "reschedule-clear-token";
        var expiresAt  = DateTimeOffset.UtcNow.AddHours(48);

        await EmitTokenAsDigestAsync(clearToken, tenantId, Guid.NewGuid(), activityId, "Reschedule", expiresAt);

        var tokenHash = ComputeHash(clearToken);

        // Act
        await using var ctx = CreateConsumerContext(tenantId);
        var adapter = new DigestActionTokenAdapter(ctx);
        var result  = await adapter.FindByHashAsync(tokenHash);

        // Assert
        result.Should().NotBeNull();
        result!.Action.Should().Be("Reschedule");
        result.ActivityId.Should().Be(activityId);
    }

    // ── Schema canônico (owned pelo digest, ADR-0006) ──────────────────────────

    private const string SharedSchemaSetupSql = @"
        CREATE TABLE IF NOT EXISTS digest_action_tokens (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            tenant_id       UUID NOT NULL,
            user_id         UUID NOT NULL,
            activity_id     UUID NOT NULL,
            action          VARCHAR(20) NOT NULL,
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
