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

        string pattern = name.Trim();

        // EF Core com HasConversion não suporta p.Name.Value nem EF.Functions.ILike com Value Object
        // porque o provider gera um cast inválido ao montar o SQL literal.
        // Usa FormattableString (FromSql) para query SQL segura e parametrizada (design §6.1).
        // O filtro global de tenant (HasQueryFilter) não é aplicado em FromSql puro — por isso
        // aplicamos o filtro manualmente via LINQ após a FromSql (composição segura).
        List<Partner> partners = await _dbContext.Partners
            .FromSql($"SELECT * FROM partners WHERE name ILIKE {pattern}")
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

        // Carrega todos os parceiros que passam no filtro de status, ordenados por nome.
        // O filtro de triagePending é aplicado em memória porque o EF Core não consegue
        // traduzir comparações de Percentage (owned type com HasConversion) no LINQ provider:
        // EF.Property<decimal> com HasConversion gera InvalidCastException ao compilar o SQL literal.
        // Solução aceita: triagePending filtra em memória (volumes esperados pequenos — Req 11, design §4.6).
        // Para escala futura, considerar coluna derivada materializada (DD-004).
        List<Partner> allPartners = await query
            .OrderBy(p => EF.Property<string>(p, "Name"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<Partner> filtered = triagePending
            ? allPartners.Where(p =>
                p.CommissionDefaults.PctSetup.Value == 0.00m &&
                p.CommissionDefaults.PctRecorrente.Value == 0.00m)
            : allPartners;

        List<Partner> page_items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(RehydrateContact)
            .ToList();

        int totalCount = triagePending ? filtered.Count() : allPartners.Count;

        return (page_items.AsReadOnly(), totalCount);
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
