using AccountManagement.Domain.Accounts;
using AccountManagement.Infrastructure.Outbox;
using AccountManagement.Infrastructure.Persistence.Configurations;
using AccountManagement.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AccountManagement.Infrastructure.Persistence;

/// <summary>
/// DbContext principal do módulo account-management.
///
/// Responsabilidades:
/// - Mapeamento das entidades <see cref="Account"/> e <see cref="Contact"/> via
///   <see cref="IEntityTypeConfiguration{TEntity}"/> (rule database-naming.md — snake_case).
/// - Filtro global de query por <c>tenant_id</c> para isolamento multi-tenant
///   (<see cref="HasQueryFilter"/> — DD-002, ADR-0001).
/// - Exposição de <see cref="OutboxMessage"/> para o relay de Outbox (DD-007).
/// - <c>DbSet</c> é interno — nunca exposto fora da Infrastructure (design §6.1).
///
/// O filtro global usa a propriedade <see cref="CurrentTenantId"/> desta instância.
/// O EF Core avalia propriedades de instância do DbContext em cada execução de query.
/// Para desabilitar o filtro (migrations/tooling/setup de testes), use
/// <c>dbSet.IgnoreQueryFilters()</c> ou o construtor sem <see cref="InfrastructureTenantContext"/>.
///
/// Mapeia: design §6.1, design §7, DD-002, ADR-0001, RNF 5, RNF 7.1, TASK-08.
/// </summary>
public sealed class AccountManagementDbContext : DbContext
{
    private readonly InfrastructureTenantContext? _tenantContext;

    // =========================================================================
    // Propriedade usada diretamente na expressão HasQueryFilter.
    // O EF Core avalia esta propriedade por instância em cada execução de query.
    // Quando não há tenant context (tooling/migrations), retorna Guid.Empty.
    // =========================================================================

    /// <summary>
    /// TenantId ativo nesta instância do DbContext.
    /// Usado exclusivamente na expressão do <see cref="HasQueryFilter"/>.
    /// </summary>
    internal Guid CurrentTenantId => _tenantContext?.TenantId ?? Guid.Empty;

    // =========================================================================
    // DbSets — internos à Infrastructure (nunca expostos publicamente)
    // =========================================================================

    /// <summary>Tabela de contas — filtrada globalmente por tenant_id.</summary>
    internal DbSet<Account> Accounts => Set<Account>();

    /// <summary>Tabela de contatos — filtrada globalmente por tenant_id.</summary>
    internal DbSet<Contact> Contacts => Set<Contact>();

    /// <summary>Tabela de mensagens do Outbox transacional (DD-007).</summary>
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Tabela de logs de auditoria imutável (append-only — RNF 8).</summary>
    internal DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    /// <summary>Tabela de chaves de idempotência (design §7).</summary>
    internal DbSet<IdempotencyKeyEntry> IdempotencyKeys => Set<IdempotencyKeyEntry>();

    // =========================================================================
    // Construtores
    // =========================================================================

    /// <summary>
    /// Construtor principal usado em runtime.
    /// O filtro global estará ativo com o <paramref name="tenantContext"/> fornecido.
    /// </summary>
    public AccountManagementDbContext(
        DbContextOptions<AccountManagementDbContext> options,
        InfrastructureTenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Construtor para migrations, tooling e setup de testes sem filtro de tenant.
    /// O filtro global usa <c>tenant_id == Guid.Empty</c> — nenhum dado real satisfaz
    /// esta condição; use <c>dbSet.IgnoreQueryFilters()</c> para bypassar o filtro
    /// quando necessário em testes de infraestrutura.
    /// </summary>
    public AccountManagementDbContext(
        DbContextOptions<AccountManagementDbContext> options)
        : base(options)
    {
        _tenantContext = null;
    }

    // =========================================================================
    // Configuração do modelo
    // =========================================================================

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica as configurações das entidades via IEntityTypeConfiguration<T>
        modelBuilder.ApplyConfiguration(new AccountEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new ContactEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntryEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyKeyEntryEntityTypeConfiguration());

        // Filtro global de tenant — defesa em profundidade (DD-002, ADR-0001, RNF 5).
        //
        // A propriedade `CurrentTenantId` é avaliada por instância do DbContext em cada
        // execução de query (EF Core avalia propriedades de instância lazy).
        //
        // Em runtime: CurrentTenantId = TenantId do JWT autenticado → filtra por tenant.
        // Em tooling/migrations: CurrentTenantId = Guid.Empty → sem dados satisfazem.
        // Para setup de testes: use IgnoreQueryFilters() ou CreateDbContextNoFilter() na fixture.
        modelBuilder.Entity<Account>()
            .HasQueryFilter(a => a.TenantId == CurrentTenantId);
        modelBuilder.Entity<Contact>()
            .HasQueryFilter(c => c.TenantId == CurrentTenantId);
    }
}
