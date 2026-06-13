using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using System.Text.Json;

namespace Organization.Infrastructure.Persistence;

/// <summary>
/// DbContext principal do módulo organization.
/// Implementa <see cref="IDatabaseContext"/> para controle transacional e propagação do tenant.
///
/// Isolamento multi-tenant — três camadas (DEC-006, ADR-0001):
/// 1. Global query filter por <c>tenant_id</c> via EF Core (filtra toda query LINQ).
/// 2. <c>SET app.current_tenant</c> na conexão (ativa o RLS do Postgres).
/// 3. Suíte de testes de isolamento como gate de CI.
///
/// O <see cref="TenantContextAccessor"/> é injetado via DI (scoped por request) para que
/// o global query filter seja avaliado com o tenant_id correto em runtime, evitando o problema
/// de captura de closure no cache do modelo EF Core.
/// </summary>
public sealed class OrganizationDbContext : DbContext, IDatabaseContext
{
    private readonly TenantContextAccessor _tenantAccessor;
    private IDbContextTransaction? _currentTransaction;

    // Opções de serialização JSON para target_memberships
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Inicializa o contexto com o acessor de tenant (injetado via DI).
    /// </summary>
    /// <param name="options">Opções EF Core.</param>
    /// <param name="tenantAccessor">
    ///   Acessor de tenant_id (scoped por request em produção; instância controlada em testes).
    /// </param>
    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options, TenantContextAccessor tenantAccessor)
        : base(options)
    {
        _tenantAccessor = tenantAccessor;
    }

    // ── DbSets ────────────────────────────────────────────────────────────────

    /// <summary>Business Units do módulo.</summary>
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();

    /// <summary>Usuários do módulo.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Convites de usuário.</summary>
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    /// <summary>Registros de Outbox para publicação de eventos.</summary>
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    /// <summary>Registros de Inbox para deduplicação de mensagens consumidas.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    // ── IDatabaseContext ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Atualiza o accessor (avaliado em runtime pelo global filter)
        _tenantAccessor.TenantId = tenantId;
        _tenantAccessor.IsEnabled = true;

        // Garante que a conexão está aberta antes de executar o SET
        await Database.OpenConnectionAsync(cancellationToken);

        // Usa FormattableString seguro — o UUID é formatado com D (apenas hex e hífens)
        // sem possibilidade de SQL injection.
#pragma warning disable EF1002 // ExecuteSqlRaw — tenant_id é Guid, sem risco de injeção
        await Database.ExecuteSqlRawAsync(
            $"SET app.current_tenant = '{tenantId:D}'",
            cancellationToken);
#pragma warning restore EF1002
    }

    /// <inheritdoc/>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException("Não há transação ativa para commit.");

        await _currentTransaction.CommitAsync(cancellationToken);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    /// <inheritdoc/>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            return;

        await _currentTransaction.RollbackAsync(cancellationToken);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    // ── SaveChanges — serialização de TargetMemberships ───────────────────────

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SerializeTargetMemberships();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SerializeTargetMemberships();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void SerializeTargetMemberships()
    {
        foreach (var entry in ChangeTracker.Entries<UserInvitation>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var memberships = entry.Entity.TargetMemberships
                .Select(m => new TargetMembershipDto { BuId = m.BuId, Role = m.Role.Value })
                .ToList();

            entry.Property("TargetMembershipsJson").CurrentValue =
                JsonSerializer.Serialize(memberships, JsonOptions);
        }
    }

    // ── Materialização de TargetMemberships após leitura ─────────────────────

    /// <summary>
    /// Desserializa <c>TargetMembershipsJson</c> para o campo privado <c>_targetMemberships</c>
    /// do agregado <see cref="UserInvitation"/> após a materialização.
    /// </summary>
    internal void RestoreTargetMemberships(UserInvitation invitation)
    {
        var entry = Entry(invitation);
        var json = entry.Property("TargetMembershipsJson").CurrentValue as string;
        if (string.IsNullOrEmpty(json)) return;

        var dtos = JsonSerializer.Deserialize<List<TargetMembershipDto>>(json, JsonOptions);
        if (dtos is null) return;

        var field = typeof(UserInvitation).GetField(
            "_targetMemberships",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var list = dtos
            .Select(dto => (dto.BuId, Role.Create(dto.Role)))
            .ToList();

        field?.SetValue(invitation, list);
    }

    // ── Configuração do modelo ────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);

        // Global query filter — Camada 1 do isolamento multi-tenant (DEC-006, ADR-0001).
        // O filtro referencia `_tenantAccessor` via closure — como `_tenantAccessor` é uma
        // instância injetada (scoped), ela é resolvida em runtime pela instância correta do contexto.
        // EF Core avalia o filtro como expressão lambda com acesso ao campo de instância,
        // garantindo que o valor do tenant_id seja o correto para cada request/contexto.
        modelBuilder.Entity<BusinessUnit>()
            .HasQueryFilter(b => !_tenantAccessor.IsEnabled || b.TenantId == _tenantAccessor.TenantId);

        modelBuilder.Entity<User>()
            .HasQueryFilter(u => !_tenantAccessor.IsEnabled || u.TenantId == _tenantAccessor.TenantId);

        modelBuilder.Entity<UserInvitation>()
            .HasQueryFilter(i => !_tenantAccessor.IsEnabled || i.TenantId == _tenantAccessor.TenantId);

        modelBuilder.Entity<OutboxEvent>()
            .HasQueryFilter(e => !_tenantAccessor.IsEnabled || e.TenantId == _tenantAccessor.TenantId);
    }

    // ── DTO interno para JSON ─────────────────────────────────────────────────

    private sealed class TargetMembershipDto
    {
        public Guid BuId { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
