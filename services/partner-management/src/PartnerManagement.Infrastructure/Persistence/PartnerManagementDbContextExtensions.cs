using Microsoft.EntityFrameworkCore;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Infrastructure.Outbox;

namespace PartnerManagement.Infrastructure.Persistence;

/// <summary>
/// Extensões do <see cref="PartnerManagementDbContext"/> para acesso controlado aos DbSets
/// em testes de integração. Os DbSets são internos — este método expõe acesso apenas
/// ao conjunto de testes via <c>InternalsVisibleTo</c>.
/// Mapeia: TASK-15, design §13.
/// </summary>
public static class PartnerManagementDbContextExtensions
{
    /// <summary>
    /// Expõe <c>Set&lt;Partner&gt;</c> para uso em testes de integração.
    /// Não deve ser usado em código de produção fora da Infrastructure.
    /// </summary>
    public static IQueryable<Partner> GetPartners(this PartnerManagementDbContext ctx)
        => ctx.Set<Partner>();

    /// <summary>
    /// Expõe <c>Set&lt;OutboxMessage&gt;</c> para uso em testes de integração.
    /// </summary>
    public static IQueryable<OutboxMessage> GetOutboxMessages(this PartnerManagementDbContext ctx)
        => ctx.Set<OutboxMessage>();

    /// <summary>
    /// Adiciona um Partner via DbContext (para uso em testes de integração).
    /// </summary>
    public static async Task AddPartnerAsync(this PartnerManagementDbContext ctx, Partner partner, CancellationToken ct = default)
    {
        await ctx.Set<Partner>().AddAsync(partner, ct);
    }
}
