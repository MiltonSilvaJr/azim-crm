using AccountManagement.Domain.Accounts.Events;
using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Infrastructure.Audit;
using AccountManagement.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountManagement.Infrastructure.Tests.Audit;

/// <summary>
/// Testes de integração para <see cref="AuditPublisher"/> com PostgreSQL real.
///
/// Cobre TASK-11 (ST-02):
/// - Persiste entrada de auditoria mascarada para cada tipo de evento de domínio.
/// - Confirma que DeltaJson gravado não contém PII em texto claro (gate RNF 1).
/// - Confirma que campos de não-PII são preservados.
/// - Confirma comportamento append-only: segunda chamada gera nova linha.
///
/// Mapeia: TASK-11 (ST-02), DD-003, DD-007, Req 8, RNF 1, RNF 8.
/// </summary>
[Collection("PostgresFixture")]
public sealed class AuditPublisherTests
{
    private readonly PostgresFixture _fixture;
    private readonly PiiMasker _piiMasker = new();

    public AuditPublisherTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // AccountCreated — persiste com DeltaJson sem PII
    // =========================================================================

    [Fact]
    public async Task PublishAsync_AccountCreated_persists_audit_entry_without_pii()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var domainEvent = new AccountCreated(
            EventId: Guid.NewGuid(),
            AccountId: accountId,
            TenantId: tenantId,
            NormalizedName: "EMPRESA TESTE",
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new AuditPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent, actorId);

        // Verifica via SQL direto para evitar filtro global de tenant
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT entity_type, entity_id, action, delta_json
            FROM audit_logs
            WHERE tenant_id = '{tenantId}' AND entity_id = '{accountId}'";

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.Read().Should().BeTrue("deve existir pelo menos uma entrada de auditoria");

        reader.GetString(0).Should().Be("Account");
        reader.GetGuid(1).Should().Be(accountId);
        reader.GetString(2).Should().Be("created");

        var deltaJson = reader.GetString(3);
        deltaJson.Should().Contain("normalizedName",
            "AccountCreated inclui normalizedName no delta");
        deltaJson.Should().Contain("EMPRESA TESTE",
            "normalizedName não é PII — deve ser preservado");
    }

    // =========================================================================
    // ContactLinked — DeltaJson nunca contém PII em texto claro
    // =========================================================================

    [Fact]
    public async Task PublishAsync_ContactLinked_persists_masked_delta_without_pii()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        // O MaskedDelta já vem mascarado do domínio (DD-003)
        const string maskedDelta = """{"name":"[anonimizado]","email":"[anonimizado]","role":"financeiro"}""";

        var domainEvent = new ContactLinked(
            EventId: Guid.NewGuid(),
            ContactId: contactId,
            AccountId: accountId,
            TenantId: tenantId,
            Action: "created",
            MaskedDelta: maskedDelta,
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new AuditPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent, actorId);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT action, delta_json
            FROM audit_logs
            WHERE tenant_id = '{tenantId}' AND entity_id = '{contactId}'";

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();

        reader.GetString(0).Should().Be("created");
        var deltaJson = reader.GetString(1);
        deltaJson.Should().Contain(ContactInfo.AnonymizationMarker,
            "DeltaJson de ContactLinked deve conter marcador de anonimização");
        deltaJson.Should().Contain("financeiro",
            "campo de não-PII 'role' deve ser preservado no delta");
    }

    // =========================================================================
    // ContactForgotten — DeltaJson contém apenas identificadores (sem PII)
    // =========================================================================

    [Fact]
    public async Task PublishAsync_ContactForgotten_persists_entry_with_only_identifiers()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();

        var domainEvent = new ContactForgotten(
            EventId: Guid.NewGuid(),
            ContactId: contactId,
            AccountId: accountId,
            TenantId: tenantId,
            RequestedBy: requestedBy,
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new AuditPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent, actorId);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT entity_type, action, delta_json
            FROM audit_logs
            WHERE tenant_id = '{tenantId}' AND entity_id = '{contactId}'
              AND action = 'forgotten'";

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.Read().Should().BeTrue("deve existir entrada de auditoria para ContactForgotten");

        reader.GetString(0).Should().Be("Contact");
        reader.GetString(1).Should().Be("forgotten");
        var deltaJson = reader.GetString(2);
        deltaJson.Should().Contain("requestedBy");
        deltaJson.Should().Contain(requestedBy.ToString());
    }

    // =========================================================================
    // AccountUpdated — ChangedFields no delta, sem PII
    // =========================================================================

    [Fact]
    public async Task PublishAsync_AccountUpdated_persists_changed_fields_without_pii()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var domainEvent = new AccountUpdated(
            EventId: Guid.NewGuid(),
            AccountId: accountId,
            TenantId: tenantId,
            ChangedFields: ["name", "website"],
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new AuditPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent, actorId);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT action, delta_json
            FROM audit_logs
            WHERE tenant_id = '{tenantId}' AND entity_id = '{accountId}'";

        await using var reader = await cmd.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();

        reader.GetString(0).Should().Be("updated");
        var deltaJson = reader.GetString(1);
        deltaJson.Should().Contain("changedFields");
        deltaJson.Should().Contain("\"name\"");
        deltaJson.Should().Contain("\"website\"");
    }

    // =========================================================================
    // Append-only: duas chamadas geram dois registros distintos
    // =========================================================================

    [Fact]
    public async Task PublishAsync_is_append_only_two_calls_generate_two_entries()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var eventFactory = () => new AccountCreated(
            EventId: Guid.NewGuid(),
            AccountId: accountId,
            TenantId: tenantId,
            NormalizedName: "EMPRESA APPEND",
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx1 = _fixture.CreateDbContext(tenantId);
        var publisher1 = new AuditPublisher(ctx1, _piiMasker);
        await publisher1.PublishAsync(eventFactory(), actorId);

        await using var ctx2 = _fixture.CreateDbContext(tenantId);
        var publisher2 = new AuditPublisher(ctx2, _piiMasker);
        await publisher2.PublishAsync(eventFactory(), actorId);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*) FROM audit_logs
            WHERE tenant_id = '{tenantId}' AND entity_id = '{accountId}'";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(2L, "AuditPublisher é append-only: cada chamada gera nova linha");
    }

    // =========================================================================
    // Gate anti-PII: DeltaJson gravado nunca vaza PII de contato
    // =========================================================================

    [Fact]
    public async Task PublishAsync_ContactLinked_delta_json_in_db_never_contains_clear_pii()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        // Simula que um desenvolvedor tentou passar PII no delta (defesa em profundidade)
        // O PiiMasker deve mascarar mesmo se o caller passar PII no MaskedDelta
        const string piiName = "Hacker Tentativa";
        const string piiEmail = "hacker@tentativa.com";
        var rawDeltaWithPii = $$"""{"name":"{{piiName}}","email":"{{piiEmail}}","role":"tester"}""";

        var domainEvent = new ContactLinked(
            EventId: Guid.NewGuid(),
            ContactId: contactId,
            AccountId: accountId,
            TenantId: tenantId,
            Action: "created",
            MaskedDelta: rawDeltaWithPii, // passa PII — publisher deve re-mascarar
            OccurredAt: DateTimeOffset.UtcNow);

        await using var ctx = _fixture.CreateDbContext(tenantId);
        var publisher = new AuditPublisher(ctx, _piiMasker);

        await publisher.PublishAsync(domainEvent, actorId);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT delta_json FROM audit_logs
            WHERE tenant_id = '{tenantId}' AND entity_id = '{contactId}'";

        var deltaJson = (string)(await cmd.ExecuteScalarAsync())!;

        _piiMasker.ContainsPii(deltaJson, [piiName, piiEmail])
            .Should().BeFalse(
                "o DeltaJson gravado em audit_logs nunca pode conter PII em texto claro (gate RNF 1)");
    }
}
