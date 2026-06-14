using Microsoft.EntityFrameworkCore;
using Organization.Application.Ports;
using Organization.Domain.Aggregates;
using Organization.Infrastructure.Persistence;

namespace Organization.Infrastructure.Repositories;

/// <summary>
/// Implementação concreta de <see cref="IBusinessUnitRepository"/> usando EF Core.
/// O isolamento por tenant é garantido pelo global query filter do <see cref="OrganizationDbContext"/>.
/// </summary>
public sealed class BusinessUnitRepository : IBusinessUnitRepository
{
    private readonly OrganizationDbContext _context;

    /// <summary>Inicializa o repositório com o contexto de banco.</summary>
    public BusinessUnitRepository(OrganizationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(BusinessUnit businessUnit, CancellationToken cancellationToken = default)
    {
        var existing = await _context.BusinessUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == businessUnit.Id, cancellationToken);

        if (existing is null)
            _context.BusinessUnits.Add(businessUnit);
        else
            _context.BusinessUnits.Update(businessUnit);
    }

    /// <inheritdoc/>
    public async Task<BusinessUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BusinessUnits
            .Include(b => b.Stages)
            .Include(b => b.OriginChannels)
            .Include(b => b.LossReasons)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsByNameAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        // b.Name é um value object (BusinessUnitName) com HasConversion(vo => vo.Value, ...).
        // EF Core associa o ValueConverter à coluna e não consegue usar ILike com parâmetro
        // string diretamente via LINQ (InvalidCastException ao sanitizar o parâmetro).
        // SqlQuery<T> com FormattableString para tipos primitivos requer coluna nomeada "Value".
        // O filtro de tenant é replicado explicitamente pois SqlQuery não passa pelo global filter.
        var count = await _context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM business_units
                WHERE tenant_id = (current_setting('app.current_tenant', true))::uuid
                  AND name ILIKE {normalizedName}
                """)
            .SingleAsync(cancellationToken);

        return count > 0;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BusinessUnit>> ListActiveAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip = (page - 1) * pageSize;
        return await _context.BusinessUnits
            .Where(b => b.Active)
            .OrderBy(b => b.Name)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
}
