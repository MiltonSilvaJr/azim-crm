namespace ActivityManagement.Infrastructure.Persistence;

using ActivityManagement.Domain.Activities;
using ActivityManagement.Infrastructure.Audit;
using ActivityManagement.Infrastructure.Outbox;
using ActivityManagement.Infrastructure.Persistence.Configurations;
using ActivityManagement.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// DbContext principal do módulo activity-management.
/// Mapeia <see cref="Activity"/> (agregado), <see cref="OutboxMessage"/>,
/// <see cref="AuditLog"/> e <see cref="DigestActionToken"/>.
/// Aplica Global Query Filter por <c>tenant_id</c> em todas as entidades multi-tenant
/// como segunda camada de isolamento (ADR-0001, DD-002).
/// O <see cref="TenantConnectionInterceptor"/> seta <c>app.current_tenant</c> por conexão
/// antes de qualquer comando — primeira camada de RLS no PostgreSQL.
/// Mapeia: design §6.1, §7, TASK-13.
/// </summary>
public sealed class ActivityManagementDbContext : DbContext
{
    // Propriedade que armazena o tenant_id corrente para o Global Query Filter.
    // Deve ser setada pelo TenantScopeBehavior ou por teste de integração antes de qualquer query.
    private Guid? _currentTenantId;

    /// <inheritdoc />
    public ActivityManagementDbContext(DbContextOptions<ActivityManagementDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Define o tenant ativo para o Global Query Filter desta instância de contexto.
    /// Chamado pelo <c>TenantScopeBehavior</c> no início de cada request.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant autenticado.</param>
    public void SetTenant(Guid tenantId) => _currentTenantId = tenantId;

    /// <summary>
    /// Expõe o tenant corrente (usado internamente e por testes de integração).
    /// </summary>
    public Guid? CurrentTenantId => _currentTenantId;

    /// <summary>Tabela de atividades comerciais.</summary>
    public DbSet<Activity> Activities => Set<Activity>();

    /// <summary>Tabela de mensagens do Outbox transacional.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Tabela de auditoria imutável (append-only).</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Tabela de tokens de ação do digest (owned pelo digest BC-06).</summary>
    public DbSet<DigestActionToken> DigestActionTokens => Set<DigestActionToken>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas as configurações de entidade via IEntityTypeConfiguration<T>
        modelBuilder.ApplyConfiguration(new ActivityConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new DigestActionTokenConfiguration());

        // Global Query Filter: isolamento por tenant (ADR-0001, DD-002)
        // Segunda camada de defesa — RLS no PostgreSQL é a primeira.
        modelBuilder.Entity<Activity>()
            .HasQueryFilter(a => _currentTenantId == null || a.TenantId == _currentTenantId);

        modelBuilder.Entity<DigestActionToken>()
            .HasQueryFilter(t => _currentTenantId == null || t.TenantId == _currentTenantId);

        // OutboxMessages e AuditLogs não possuem Global Query Filter por tenant
        // pois são acessadas pelo relay e por administração — RLS aplica no banco.
    }
}
