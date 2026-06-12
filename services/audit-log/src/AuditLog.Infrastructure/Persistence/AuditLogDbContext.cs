using AuditLog.Application.Abstractions;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuditLog.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Audit Log.
/// Aplica filtro global de <c>tenant_id</c> em todas as queries (DD-003, REQ-005),
/// garantindo isolamento por tenant na camada de aplicação (defense-in-depth — segunda camada).
/// A terceira camada de isolamento é o RLS no banco (design §14, migration TASK-12).
/// <para>
/// O filtro de tenant usa <see cref="CurrentTenantId"/> (propriedade de instância) para ser
/// avaliado em runtime sem forçar a recriação do service provider do EF Core.
/// </para>
/// </summary>
public sealed class AuditLogDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    /// <summary>DbSet de registros de auditoria (append-only).</summary>
    public DbSet<AuditLogAggregate> AuditLogs => Set<AuditLogAggregate>();

    /// <summary>
    /// Tenant ID corrente, usado pelo filtro global (avaliado em runtime, não no modelo).
    /// </summary>
    public Guid? CurrentTenantId => _tenantContext.TenantId;

    /// <summary>
    /// Inicializa o contexto com as opções e o contexto de tenant corrente.
    /// </summary>
    public AuditLogDbContext(DbContextOptions<AuditLogDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Constrói as options padrão para o DbContext.
    /// Use este método para garantir consistência entre instâncias e evitar
    /// a recriação de service providers do EF Core.
    /// </summary>
    public static DbContextOptionsBuilder<AuditLogDbContext> CreateOptionsBuilder(string connectionString)
    {
        return new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica configurações de entidade
        modelBuilder.ApplyConfiguration(new AuditLogEntityConfiguration());

        // Filtro global de tenant — DD-003.
        // HasQueryFilter usa propriedade de instância CurrentTenantId para avaliação em runtime.
        // Compara o value object TenantId com uma instância construída via TenantId.From().
        // O ValueConverter<TenantId, Guid> explícito garante que o Sanitize funcione corretamente.
        modelBuilder.Entity<AuditLogAggregate>()
            .HasQueryFilter(x => CurrentTenantId == null ||
                x.TenantId == TenantId.From(CurrentTenantId.Value));
    }

    /// <summary>
    /// Sobrescreve SaveChanges para garantir que nenhuma operação de UPDATE ou DELETE
    /// seja enviada para <c>audit_logs</c> (RNF-001, DD-002).
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc cref="SaveChanges(bool)"/>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // ------------------------------------------------------------------ Guards

    private void EnforceAppendOnly()
    {
        var illegalEntries = ChangeTracker.Entries<AuditLogAggregate>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (illegalEntries.Count > 0)
            throw new InvalidOperationException(
                "Operação proibida: audit_logs é append-only. " +
                "UPDATE e DELETE são rejeitados pelo DbContext (RNF-001, DD-002). " +
                $"Entradas inválidas: {illegalEntries.Count}.");
    }
}
