using Microsoft.EntityFrameworkCore;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;

namespace PartnerManagement.Infrastructure.Persistence;

/// <summary>
/// Implementação concreta de <see cref="IPartnerRepository"/> usando EF Core.
/// Não vaza <c>IQueryable</c> nem <c>DbSet</c> para fora da Infrastructure (design §6.1).
/// Todas as queries são escopadas ao tenant via filtro global + RLS (DD-001, ADR-0001).
/// Contact é persistido como shadow properties e rehidratado via <c>Partner.Reconstitute</c>.
/// Mapeia: design §6.1, RNF 1, DD-001, TASK-17.
/// </summary>
public sealed class PartnerRepository : IPartnerRepository
{
    private readonly PartnerManagementDbContext _dbContext;

    /// <summary>
    /// Inicializa o repositório com o DbContext scoped.
    /// </summary>
    /// <param name="dbContext">DbContext com filtro global de tenant ativo.</param>
    public PartnerRepository(PartnerManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<Partner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Partner? partner = await _dbContext.Partners
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (partner is null)
        {
            return null;
        }

        // Rehidrata PartnerContact a partir das shadow properties (contact_email, contact_phone)
        return RehydrateContact(partner);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        await _dbContext.Partners.AddAsync(partner, cancellationToken).ConfigureAwait(false);

        // Define shadow properties de contato no entry
        SetContactShadowProperties(partner);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        _dbContext.Partners.Update(partner);

        // Atualiza shadow properties de contato no entry
        SetContactShadowProperties(partner);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Partner>> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        List<Partner> partners = await _dbContext.Partners
            .Where(p => EF.Functions.ILike(p.Name.Value, name.Trim()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return partners.Select(RehydrateContact).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Partner> Partners, int TotalCount)> ListAsync(
        bool? active,
        bool triagePending,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Partner> query = _dbContext.Partners;

        if (active.HasValue)
        {
            PartnerStatus targetStatus = active.Value ? PartnerStatus.Active : PartnerStatus.Inactive;
            query = query.Where(p => p.Status == targetStatus);
        }

        // Filtro por triagem pendente (percentuais em 0,00 — Req 11, design §4.6)
        if (triagePending)
        {
            query = query.Where(p =>
                p.CommissionDefaults.PctSetup.Value == 0.00m &&
                p.CommissionDefaults.PctRecorrente.Value == 0.00m);
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<Partner> partners = await query
            .OrderBy(p => p.Name.Value)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (partners.Select(RehydrateContact).ToList().AsReadOnly(), totalCount);
    }

    // =========================================================================
    // Helpers para shadow properties de PartnerContact
    // =========================================================================

    /// <summary>
    /// Define as shadow properties <c>ContactEmail</c> e <c>ContactPhone</c> no entry do EF Core.
    /// Chamado em Add e Update para persistir o contato.
    /// </summary>
    private void SetContactShadowProperties(Partner partner)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Partner> entry = _dbContext.Entry(partner);
        entry.Property<string?>("ContactEmail").CurrentValue = partner.Contact?.EmailAddress?.Value;
        entry.Property<string?>("ContactPhone").CurrentValue = partner.Contact?.PhoneNumber?.Value;
    }

    /// <summary>
    /// Rehidrata o <see cref="PartnerContact"/> a partir das shadow properties lidas do banco.
    /// Retorna o mesmo parceiro com Contact reconstruído (via Reconstitute interno).
    /// </summary>
    private Partner RehydrateContact(Partner partner)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Partner> entry = _dbContext.Entry(partner);

        string? emailValue = entry.Property<string?>("ContactEmail").CurrentValue;
        string? phoneValue = entry.Property<string?>("ContactPhone").CurrentValue;

        if (emailValue is null && phoneValue is null)
        {
            return partner; // sem contato — retorna o parceiro como está
        }

        PartnerContact contact = PartnerContact.Create(emailValue, phoneValue);

        // Usa Reconstitute para recriar com contato (não acumula eventos)
        return Partner.Reconstitute(
            partner.Id,
            partner.TenantId,
            partner.Name,
            partner.Role,
            partner.CommissionDefaults,
            contact,
            partner.Notes,
            partner.Status,
            partner.CreatedAt,
            partner.CreatedBy,
            partner.UpdatedAt,
            partner.UpdatedBy);
    }
}
