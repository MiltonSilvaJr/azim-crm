using DataMigration.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace DataMigration.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo data-migration.
///
/// Responsabilidades:
/// - Mapeia <see cref="MigrationJob"/> e <see cref="MigrationLogEntry"/> via Fluent API.
/// - Aplica Global Query Filter por <c>tenant_id</c> (ADR-0001, design §6.1).
/// - O interceptor <see cref="TenantConnectionInterceptor"/> seta <c>app.current_tenant</c>
///   antes de qualquer comando SQL (DD-008).
///
/// Rastreia: design §6.1, §7, DD-008, ADR-0001, TASK-15.
/// </summary>
public sealed class MigrationDbContext : DbContext
{
    private readonly Guid _currentTenantId;

    /// <summary>
    /// Inicializa o contexto com o tenant corrente (ADR-0001).
    /// </summary>
    /// <param name="options">Opções do DbContext.</param>
    /// <param name="currentTenantId">ID do tenant corrente para Global Query Filter e RLS.</param>
    public MigrationDbContext(DbContextOptions<MigrationDbContext> options, Guid currentTenantId)
        : base(options)
    {
        if (currentTenantId == Guid.Empty)
        {
            throw new ArgumentException("currentTenantId não pode ser Empty (ADR-0001).", nameof(currentTenantId));
        }

        _currentTenantId = currentTenantId;
    }

    /// <summary>Jobs de migração.</summary>
    public DbSet<MigrationJob> MigrationJobs => Set<MigrationJob>();

    /// <summary>Entradas de log de processamento de linhas.</summary>
    public DbSet<MigrationLogEntry> MigrationLogs => Set<MigrationLogEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMigrationJob(modelBuilder);
        ConfigureMigrationLogEntry(modelBuilder);
    }

    // =========================================================================
    // Configuração de entidades via Fluent API
    // =========================================================================

    private void ConfigureMigrationJob(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationJob>(entity =>
        {
            entity.ToTable("migration_jobs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired()
                .HasConversion(
                    v => v.ToSnakeCase(),
                    v => MigrationJobStatusExtensions.FromSnakeCase(v));

            entity.Property(e => e.SourceFileName)
                .HasColumnName("source_file_name")
                .IsRequired();

            entity.Property(e => e.SourceFileSizeBytes)
                .HasColumnName("source_file_size")
                .IsRequired();

            entity.Property(e => e.SourceFileHash)
                .HasColumnName("source_file_hash")
                .IsRequired();

            entity.Property(e => e.DetectedRowCount)
                .HasColumnName("detected_row_count")
                .IsRequired();

            entity.Property(e => e.TriageReportJson)
                .HasColumnName("triage_report")
                .HasColumnType("jsonb");

            entity.Property(e => e.TriageResolutionJson)
                .HasColumnName("triage_resolution")
                .HasColumnType("jsonb");

            entity.Property(e => e.ImportReportJson)
                .HasColumnName("import_report")
                .HasColumnType("jsonb");

            entity.Property(e => e.StartedAt)
                .HasColumnName("started_at")
                .HasColumnType("timestamptz");

            entity.Property(e => e.FinishedAt)
                .HasColumnName("finished_at")
                .HasColumnType("timestamptz");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("now()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("now()");

            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by")
                .IsRequired();

            // CHECK constraint de status.
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_migration_status",
                "status IN ('created','dry_run_completed','triage_in_progress','ready_to_import','importing','completed','rolled_back','failed')"));

            // Índice de busca por tenant + status (design §7).
            entity.HasIndex(e => new { e.TenantId, e.Status })
                .HasDatabaseName("idx_migration_jobs_status");

            // Global Query Filter por tenant_id (ADR-0001).
            entity.HasQueryFilter(e => e.TenantId == _currentTenantId);

            // Relacionamento com log entries — EF Core 9 localiza o campo backing _logEntries
            // por convenção de nome (_<Property>) quando a propriedade é IReadOnlyList.
            entity.HasMany(e => e.LogEntries)
                .WithOne()
                .HasForeignKey(nameof(MigrationLogEntry.MigrationJobId))
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(e => e.LogEntries).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private void ConfigureMigrationLogEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationLogEntry>(entity =>
        {
            entity.ToTable("migration_logs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            // tenant_id é shadow property — não existe em MigrationLogEntry mas é obrigatório
            // na tabela (ADR-0001, DD-008). Será setado pelo SaveChangesAsync via interceptor.
            entity.Property<Guid>("TenantId")
                .HasColumnName("tenant_id")
                .IsRequired();

            entity.Property(e => e.MigrationJobId)
                .HasColumnName("migration_job_id")
                .IsRequired();

            entity.Property(e => e.SourceSheet)
                .HasColumnName("source_sheet")
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(e => e.SourceRowIndex)
                .HasColumnName("source_row_index")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(10)
                .IsRequired()
                .HasConversion(
                    v => v.ToSnakeCase(),
                    v => MigrationLogStatusExtensions.FromSnakeCase(v));

            entity.Property(e => e.Message)
                .HasColumnName("message")
                .IsRequired();

            entity.Property(e => e.ImportKey)
                .HasColumnName("import_key");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired()
                .HasDefaultValueSql("now()");

            // CHECK constraint de status.
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_migration_log_status",
                "status IN ('ok','aviso','erro')"));

            // Índice de busca por job + linha (design §7).
            // Usa nomes em string pois TenantId é shadow property (não pode usar EF.Property em HasIndex lambda).
            entity.HasIndex("TenantId", nameof(MigrationLogEntry.MigrationJobId), nameof(MigrationLogEntry.SourceRowIndex))
                .HasDatabaseName("idx_migration_logs_job");

            // Índice único parcial para import_key de idempotência (DD-003, design §7).
            entity.HasIndex("TenantId", nameof(MigrationLogEntry.ImportKey))
                .HasDatabaseName("uq_migration_log_import_key")
                .IsUnique()
                .HasFilter("import_key IS NOT NULL");

            // Global Query Filter por shadow property tenant_id (ADR-0001).
            entity.HasQueryFilter(e => EF.Property<Guid>(e, "TenantId") == _currentTenantId);
        });
    }
}
