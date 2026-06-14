using Digest.Domain.Entities;
using Digest.Infrastructure.Messaging;
using Digest.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Digest.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo digest.
/// Contém apenas as entidades próprias do BC: <see cref="EmailDigestLog"/>, <see cref="DigestActionToken"/>
/// e <see cref="OutboxMessage"/>. Proibido mapear tabelas de outros BCs (Req 6.2, Architecture.Tests).
/// Global Query Filter por <c>tenant_id</c> em ambas as entidades (ADR-0001, design §6.1).
/// O filtro de tenant é a primeira camada de defesa em profundidade; RLS é a segunda (DD-002).
/// </summary>
public sealed class DigestDbContext : DbContext
{
    // Contexto de tenant injetado — null antes de setar app.current_tenant
    private readonly ITenantContext _tenantContext;

    /// <summary>Registros de envio do digest — base de idempotência (RN-010).</summary>
    public DbSet<EmailDigestLog> EmailDigestLogs => Set<EmailDigestLog>();

    /// <summary>Tokens de ação de um clique (token_hash apenas — DD-007).</summary>
    public DbSet<DigestActionToken> DigestActionTokens => Set<DigestActionToken>();

    /// <summary>Outbox transacional para publicação de <c>DigestEmailSent</c> (DD-009).</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Construtor primário para injeção de dependência.
    /// </summary>
    public DigestDbContext(DbContextOptions<DigestDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas as configurações via IEntityTypeConfiguration<T>
        modelBuilder.ApplyConfiguration(new EmailDigestLogConfiguration());
        modelBuilder.ApplyConfiguration(new DigestActionTokenConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // ------------------------------------------------------------------
        // Global Query Filter por tenant_id (ADR-0001, design §6.1)
        // Primeira camada de defesa em profundidade.
        // RLS (segunda camada) é aplicada via TenantConnectionInterceptor (TASK-15).
        // ------------------------------------------------------------------
        modelBuilder.Entity<EmailDigestLog>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.CurrentTenantId);

        modelBuilder.Entity<DigestActionToken>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.CurrentTenantId);

        // OutboxMessage não tem Global Query Filter — o relay lê cross-tenant
    }
}
