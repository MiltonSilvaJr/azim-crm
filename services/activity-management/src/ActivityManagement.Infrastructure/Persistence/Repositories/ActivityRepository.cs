namespace ActivityManagement.Infrastructure.Persistence.Repositories;

using System.Data;
using ActivityManagement.Domain.Activities;
using ActivityManagement.Domain.Activities.Repositories;
using ActivityManagement.Domain.Activities.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// Implementação de <see cref="IActivityRepository"/> sobre EF Core + PostgreSQL.
/// Não expõe <c>IQueryable</c> nem <c>DbSet</c> para fora desta camada (design §6.1).
/// Todos os filtros por tenant são aplicados pelo Global Query Filter do contexto
/// (segunda camada) + RLS no PostgreSQL (primeira camada — DD-002, ADR-0001).
///
/// As queries de leitura usam SQL raw via <see cref="DbContext.Database.GetDbConnection"/>
/// para evitar o problema de Value Objects com conversores no LINQ → SQL do EF Core.
/// A persistência (SaveAsync) usa EF Core normalmente via <see cref="DbSet{TEntity}"/>.
/// Mapeia: design §6.1, TASK-13.
/// </summary>
internal sealed class ActivityRepository : IActivityRepository
{
    private readonly ActivityManagementDbContext _context;

    public ActivityRepository(ActivityManagementDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Activity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        var tracked = _context.ChangeTracker.Entries<Activity>()
            .FirstOrDefault(e => e.Entity.Id == activity.Id);

        if (tracked is not null)
        {
            tracked.CurrentValues.SetValues(activity);
        }
        else
        {
            var exists = await _context.Activities
                .AsNoTracking()
                .AnyAsync(a => a.Id == activity.Id, cancellationToken)
                .ConfigureAwait(false);

            if (!exists)
                _context.Activities.Add(activity);
            else
                _context.Activities.Update(activity);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (activity is not null)
        {
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Activity>> GetOverduePageAsync(
        DateTimeOffset    referenceInstant,
        int               skip,
        int               take,
        CancellationToken cancellationToken = default)
    {
        // Usa SQL raw para evitar problemas de tradução de Value Objects com conversores
        var ids = await QueryScalarAsync<Guid>(
            @"SELECT id FROM activities
              WHERE status NOT IN ('completed','cancelled')
                AND due_at < @p0
              ORDER BY due_at
              LIMIT @p1 OFFSET @p2",
            cancellationToken,
            referenceInstant, take, skip);

        return await LoadByIdsAsync(ids, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, Activity>> GetLastCompletedByOpportunitiesAsync(
        IReadOnlyList<Guid> opportunityIds,
        CancellationToken   cancellationToken = default)
    {
        if (opportunityIds.Count == 0)
            return new Dictionary<Guid, Activity>();

        // SQL que usa o índice (tenant_id, opportunity_id, completed_at)
        var idsArray = opportunityIds.ToArray();

        var rows = await QueryRawAsync<(Guid ActivityId, Guid OpportunityId)>(
            @"SELECT DISTINCT ON (opportunity_id) id, opportunity_id
              FROM activities
              WHERE status = 'completed'
                AND opportunity_id = ANY(@p0)
              ORDER BY opportunity_id, completed_at DESC",
            reader => (reader.GetGuid(0), reader.GetGuid(1)),
            cancellationToken,
            (object)idsArray);

        if (rows.Count == 0)
            return new Dictionary<Guid, Activity>();

        var activityIds = rows.Select(r => r.ActivityId).ToList();
        var activities = await _context.Activities
            .Where(a => activityIds.Contains(a.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Join(activities, r => r.ActivityId, a => a.Id, (r, a) => (r.OpportunityId, Activity: a))
            .ToDictionary(x => x.OpportunityId, x => x.Activity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Activity>> GetByOwnerInRangeAsync(
        Guid              ownerId,
        DateTimeOffset    from,
        DateTimeOffset    to,
        CancellationToken cancellationToken = default)
    {
        var ids = await QueryScalarAsync<Guid>(
            @"SELECT id FROM activities
              WHERE owner_id = @p0
                AND status NOT IN ('completed','cancelled')
                AND due_at >= @p1
                AND due_at < @p2
              ORDER BY due_at",
            cancellationToken,
            ownerId, from, to);

        return await LoadByIdsAsync(ids, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Activity>> GetOverdueByOwnerAsync(
        Guid              ownerId,
        DateTimeOffset    referenceInstant,
        CancellationToken cancellationToken = default)
    {
        var ids = await QueryScalarAsync<Guid>(
            @"SELECT id FROM activities
              WHERE owner_id = @p0
                AND status NOT IN ('completed','cancelled')
                AND due_at < @p1
              ORDER BY due_at",
            cancellationToken,
            ownerId, referenceInstant);

        return await LoadByIdsAsync(ids, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Activity> Items, int Total)> ListAsync(
        ActivityListFilter filter,
        CancellationToken  cancellationToken = default)
    {
        // Para ListAsync usamos EF Core com filtros em memória após carregamento inicial
        // para suportar todos os filtros opcionais sem queries SQL dinâmicas complexas.
        // Casos de volume alto devem usar Dapper ou FromSqlRaw com parâmetros.
        var query = _context.Activities.AsQueryable();

        if (filter.OwnerId.HasValue)
            query = query.Where(a => a.OwnerId == filter.OwnerId.Value);

        // Conta total antes de paginar
        var allItems = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        // Aplica filtros em memória (Value Objects)
        IEnumerable<Activity> filtered = allItems;

        if (!string.IsNullOrEmpty(filter.Type))
            filtered = filtered.Where(a => a.Type.Value == filter.Type);

        if (!string.IsNullOrEmpty(filter.Status))
            filtered = filtered.Where(a => a.Status.Value == filter.Status);

        if (filter.OnlyOverdue)
        {
            var now = DateTimeOffset.UtcNow;
            filtered = filtered.Where(a => !a.Status.IsTerminal && a.DueAt.Value < now);
        }

        if (filter.OpportunityId.HasValue)
            filtered = filtered.Where(a =>
                a.OpportunityLink != null
                && a.OpportunityLink.OpportunityId == filter.OpportunityId.Value);

        var filteredList = filtered.OrderBy(a => a.DueAt.Value).ToList();
        var total        = filteredList.Count;
        var items        = filteredList
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return (items.AsReadOnly(), total);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetOpportunityIdsWithoutFollowupAsync(
        Guid                buId,
        IReadOnlyList<Guid> openOpportunityIds,
        DateTimeOffset      referenceInstant,
        CancellationToken   cancellationToken = default)
    {
        if (openOpportunityIds.Count == 0)
            return [];

        var openOppIds = openOpportunityIds.ToArray();

        var opportunitiesWithFollowup = await QueryScalarAsync<Guid>(
            @"SELECT DISTINCT opportunity_id FROM activities
              WHERE bu_id = @p0
                AND status NOT IN ('completed','cancelled')
                AND due_at > @p1
                AND opportunity_id = ANY(@p2)",
            cancellationToken,
            buId, referenceInstant, (object)openOppIds);

        var withFollowupSet = opportunitiesWithFollowup.ToHashSet();
        return openOpportunityIds
            .Where(id => !withFollowupSet.Contains(id))
            .ToList()
            .AsReadOnly();
    }

    // ── Helpers privados ──────────────────────────────────────────────────────

    /// <summary>
    /// Carrega atividades por lista de IDs mantendo a ordem.
    /// </summary>
    private async Task<IReadOnlyList<Activity>> LoadByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken   ct)
    {
        if (ids.Count == 0)
            return [];

        var activities = await _context.Activities
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Preserva a ordem retornada pelo SQL
        var byId = activities.ToDictionary(a => a.Id);
        return ids.Where(id => byId.ContainsKey(id)).Select(id => byId[id]).ToList().AsReadOnly();
    }

    /// <summary>
    /// Executa uma query SQL raw e retorna uma lista de escalares do tipo <typeparamref name="T"/>.
    /// </summary>
    private async Task<IReadOnlyList<T>> QueryScalarAsync<T>(
        string sql, CancellationToken ct, params object[] parameters)
    {
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen)
            await conn.OpenAsync(ct).ConfigureAwait(false);

        try
        {
            await using var cmd = conn.CreateCommand();
            // Não propaga transaction aqui — os comandos auxiliares são read-only
            cmd.CommandText = BuildSqlWithParameters(sql, parameters, cmd);

            var results = new List<T>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                results.Add((T)reader.GetValue(0));

            return results;
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executa uma query SQL raw e retorna uma lista de tuplas mapeadas pelo <paramref name="mapper"/>.
    /// </summary>
    private async Task<IReadOnlyList<TResult>> QueryRawAsync<TResult>(
        string sql,
        Func<IDataReader, TResult> mapper,
        CancellationToken ct,
        params object[] parameters)
    {
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen)
            await conn.OpenAsync(ct).ConfigureAwait(false);

        try
        {
            await using var cmd = conn.CreateCommand();
            // Não propaga transaction aqui — os comandos auxiliares são read-only
            cmd.CommandText = BuildSqlWithParameters(sql, parameters, cmd);

            var results = new List<TResult>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                results.Add(mapper(reader));

            return results;
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Substitui os placeholders <c>@p0</c>, <c>@p1</c>, etc. por parâmetros reais no comando.
    /// </summary>
    private static string BuildSqlWithParameters(string sql, object[] parameters, IDbCommand cmd)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = $"p{i}";
            if (parameters[i] is Guid[] guids)
            {
                // Npgsql precisa de NpgsqlParameter tipado para arrays
                var npgsqlParam = new NpgsqlParameter<Guid[]>($"p{i}", guids);
                if (cmd is NpgsqlCommand npgsqlCmd)
                {
                    npgsqlCmd.Parameters.Add(npgsqlParam);
                    continue;
                }
            }
            param.Value = parameters[i] is DateTimeOffset dto
                ? dto.ToUniversalTime()
                : parameters[i];
            cmd.Parameters.Add(param);
        }
        return sql;
    }
}
