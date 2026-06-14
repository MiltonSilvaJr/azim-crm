using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;
using PartnerManagement.Infrastructure.Audit;
using PartnerManagement.Infrastructure.Outbox;
using PartnerManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PartnerManagement.Infrastructure.Tests.Outbox;

/// <summary>
/// Testes de integração para Outbox transacional e UnitOfWork (TASK-18).
/// Verifica: parceiro + outbox_message criados na mesma transação (atomicidade),
/// rollback descarta ambos, relay marca published_at, AuditPublisher mascara PII.
/// Usa PostgreSQL real via Testcontainers.
/// Mapeia: RNF 2, RNF 3, RNF 4, design §6.3, design §6.6, TASK-18.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OutboxAtomicityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string _connectionString = null!;

    private static readonly ICanonicalRoleProvider RoleProvider = new AlwaysValidRoleProvider();
    private static readonly Guid ActorId = Guid.NewGuid();

    public OutboxAtomicityTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("partner_management_outbox_test")
            .WithUsername("app_test")
            .WithPassword("test_secret")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        using PartnerManagementDbContext ctx = CreateDbContext(Guid.NewGuid());
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    // =========================================================================
    // Atomicidade: parceiro + OutboxMessage na mesma transação
    // =========================================================================

    /// <summary>
    /// TASK-18 ST-01: partner e outbox_message são persistidos na mesma transação.
    /// Após commit ambos existem; após rollback nenhum existe.
    /// </summary>
    [Fact(DisplayName = "TASK-18: partner e outbox_message são criados atomicamente")]
    public async Task Partner_And_OutboxMessage_AreCreatedAtomically()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Partner partner = Partner.Create(
            tenantId, "Parceiro Outbox", "Indicador",
            CommissionDefaults.Create(Percentage.Create(0m), Percentage.Create(0m)),
            null, null, RoleProvider, ActorId);

        OutboxMessage outbox = OutboxMessage.Create(
            tenantId,
            eventType: "PartnerCreated",
            payloadJson: """{"id":"test","event":"PartnerCreated"}""",
            occurredAt: DateTimeOffset.UtcNow);

        // Act — salva parceiro + outbox na mesma SaveChanges (sem transação explícita)
        await using PartnerManagementDbContext ctx = CreateDbContext(tenantId);
        PartnerRepository repo = new(ctx);
        await repo.AddAsync(partner);
        await ctx.Set<OutboxMessage>().AddAsync(outbox);
        await ctx.SaveChangesAsync();

        // Assert — ambos devem existir no banco
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        Partner? foundPartner = await ctxRead.GetPartners().FirstOrDefaultAsync(p => p.Id == partner.Id);
        OutboxMessage? foundOutbox = await ctxRead.GetOutboxMessages().FirstOrDefaultAsync(o => o.Id == outbox.Id);

        foundPartner.Should().NotBeNull("o parceiro deve ter sido salvo");
        foundOutbox.Should().NotBeNull("a mensagem de outbox deve ter sido salva na mesma operação");
        foundOutbox!.PublishedAt.Should().BeNull("outbox ainda não foi publicado pelo relay");
    }

    /// <summary>
    /// TASK-18 ST-01: rollback de transação descarta parceiro e outbox_message.
    /// </summary>
    [Fact(DisplayName = "TASK-18: rollback descarta parceiro e outbox_message")]
    public async Task Rollback_DiscardsPartnerAndOutboxMessage()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Partner partner = Partner.Create(
            tenantId, "Parceiro Rollback", "Revendedor",
            CommissionDefaults.Create(Percentage.Create(0m), Percentage.Create(0m)),
            null, null, RoleProvider, ActorId);

        OutboxMessage outbox = OutboxMessage.Create(
            tenantId,
            eventType: "PartnerCreated",
            payloadJson: """{"id":"rollback","event":"PartnerCreated"}""",
            occurredAt: DateTimeOffset.UtcNow);

        // Act — inicia transação, adiciona mas faz rollback
        await using PartnerManagementDbContext ctx = CreateDbContext(tenantId);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx =
            await ctx.Database.BeginTransactionAsync();

        PartnerRepository repo = new(ctx);
        await repo.AddAsync(partner);
        await ctx.Set<OutboxMessage>().AddAsync(outbox);
        await ctx.SaveChangesAsync();

        // Rollback sem commit
        await tx.RollbackAsync();

        // Assert — nenhum dos dois deve existir
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        Partner? foundPartner = await ctxRead.GetPartners().FirstOrDefaultAsync(p => p.Id == partner.Id);
        OutboxMessage? foundOutbox = await ctxRead.GetOutboxMessages().FirstOrDefaultAsync(o => o.Id == outbox.Id);

        foundPartner.Should().BeNull("o rollback deve ter descartado o parceiro");
        foundOutbox.Should().BeNull("o rollback deve ter descartado a mensagem de outbox");
    }

    // =========================================================================
    // Relay: MarkAsPublished
    // =========================================================================

    /// <summary>
    /// TASK-18: relay marca outbox_message como publicado com published_at preenchido.
    /// </summary>
    [Fact(DisplayName = "TASK-18: relay marca outbox_message como publicado")]
    public async Task Relay_MarksOutboxMessage_AsPublished()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        OutboxMessage outbox = OutboxMessage.Create(
            tenantId,
            eventType: "PartnerUpdated",
            payloadJson: """{"id":"relay-test"}""",
            occurredAt: DateTimeOffset.UtcNow);

        await using (PartnerManagementDbContext ctxWrite = CreateDbContext(tenantId))
        {
            await ctxWrite.Set<OutboxMessage>().AddAsync(outbox);
            await ctxWrite.SaveChangesAsync();
        }

        // Act — simula relay que marca como publicado
        DateTimeOffset publishedAt = DateTimeOffset.UtcNow;

        await using (PartnerManagementDbContext ctxRelay = CreateDbContext(tenantId))
        {
            OutboxMessage? pending = await ctxRelay.GetOutboxMessages()
                .FirstOrDefaultAsync(o => o.Id == outbox.Id);

            pending.Should().NotBeNull();
            pending!.MarkAsPublished(publishedAt);
            await ctxRelay.SaveChangesAsync();
        }

        // Assert — published_at deve estar preenchido
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        OutboxMessage? published = await ctxRead.GetOutboxMessages()
            .FirstOrDefaultAsync(o => o.Id == outbox.Id);

        published.Should().NotBeNull();
        published!.PublishedAt.Should().NotBeNull("o relay deve marcar published_at");
        published.PublishedAt.Should().BeCloseTo(publishedAt, TimeSpan.FromSeconds(1));
    }

    // =========================================================================
    // AuditPublisher: PII masking
    // =========================================================================

    /// <summary>
    /// TASK-18: AuditPublisher mascara PII (email, phone) no payload da outbox_message.
    /// </summary>
    [Fact(DisplayName = "TASK-18: AuditPublisher mascara PII no payload da outbox")]
    public async Task AuditPublisher_MasksContactPii_InOutboxPayload()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        PartnerPiiMasker masker = new();

        string deltaJson = """
            {
                "name": "João Silva",
                "contact_email": "joao.silva@empresa.com.br",
                "contact_phone": "+5511987654321"
            }
            """;

        // Act — AuditPublisher mascara o deltaJson antes de criar OutboxMessage
        await using PartnerManagementDbContext ctx = CreateDbContext(tenantId);
        AuditPublisher auditPublisher = new(ctx, masker);

        await auditPublisher.PublishAsync(
            entityType: "Partner",
            entityId: Guid.NewGuid(),
            tenantId: tenantId,
            action: "updated",
            deltaJson: deltaJson,
            actorId: ActorId,
            correlationId: "corr-123");

        await ctx.SaveChangesAsync();

        // Assert — payload gravado no banco não deve conter email/phone em claro
        await using PartnerManagementDbContext ctxRead = CreateDbContext(tenantId);
        OutboxMessage? saved = await ctxRead.GetOutboxMessages()
            .OrderByDescending(o => o.OccurredAt)
            .FirstOrDefaultAsync();

        saved.Should().NotBeNull();
        saved!.PayloadJson.Should().NotContain("joao.silva@empresa.com.br", "email deve ser mascarado");
        saved.PayloadJson.Should().NotContain("+5511987654321", "telefone deve ser mascarado");
        saved.EventType.Should().Be("audit.partner.updated");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private PartnerManagementDbContext CreateDbContext(Guid tenantId)
    {
        DbContextOptions<PartnerManagementDbContext> options = new DbContextOptionsBuilder<PartnerManagementDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new PartnerManagementDbContext(options, new StaticTenantContext(tenantId));
    }

    private sealed class AlwaysValidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => true;
    }

    private sealed class StaticTenantContext : Application.Ports.ITenantContext
    {
        public StaticTenantContext(Guid tenantId) => CurrentTenantId = tenantId;
        public Guid CurrentTenantId { get; }
    }
}
