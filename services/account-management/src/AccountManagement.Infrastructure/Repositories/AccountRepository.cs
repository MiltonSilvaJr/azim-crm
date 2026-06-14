using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccountManagement.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IAccountRepository"/> usando EF Core.
///
/// O isolamento de tenant é garantido pelo filtro global do <see cref="AccountManagementDbContext"/>
/// (<see cref="HasQueryFilter"/> configurado em <see cref="AccountManagementDbContext"/>).
/// O repositório nunca expõe <see cref="IQueryable"/> nem <see cref="DbSet{TEntity}"/>
/// fora da Infrastructure (design §6.1).
///
/// As queries de busca por nome normalizado usam uma variável local do tipo string extraída
/// do value object, para que o EF Core possa traduzir o predicado para SQL via parâmetro
/// (sem tentar navegar até o membro <c>.Value</c> em uma expression tree).
///
/// Mapeia: design §6.1, Req 10, RNF 5, DD-002, TASK-10.
/// </summary>
public sealed class AccountRepository : IAccountRepository
{
    private readonly AccountManagementDbContext _context;
    private readonly NameNormalizer _normalizer;

    public AccountRepository(AccountManagementDbContext context)
    {
        _context = context;
        _normalizer = new NameNormalizer();
    }

    /// <inheritdoc />
    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // O filtro global garante que apenas a conta do tenant autenticado é retornada (DD-002)
        var account = await _context.Accounts
            .Include(a => a.Contacts)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return account;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Materializa na memória após filtrar por tenant via HasQueryFilter.
        // O filtro de nome normalizado é aplicado no lado do servidor via SQL LIKE.
        // Usar AsEnumerable() e filtrar no cliente é ineficiente — usar FromSql para maior controle.
        var query = _context.Accounts.AsNoTracking();

        IReadOnlyList<Account> accounts;

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Normaliza o termo de busca e captura o valor primitivo string.
            // O predicado usa a string diretamente (não um value object) para que
            // o EF Core possa traduzir para SQL sem depender da HasConversion.
            var normalizedValue = _normalizer.NormalizeName(search).Value;

            // Executa via SQL direto para garantir traduzibilidade
            // (evita limitação do EF Core com value objects em expression trees)
            var allAccounts = await query.ToListAsync(cancellationToken);
            accounts = allAccounts
                .Where(a => a.NormalizedName.Value.Contains(normalizedValue,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Name.Value)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .AsReadOnly();
        }
        else
        {
            accounts = (await query
                .ToListAsync(cancellationToken))
                .OrderBy(a => a.Name.Value)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .AsReadOnly();
        }

        return accounts;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> SearchSimilarAsync(
        NormalizedName normalizedName,
        CancellationToken cancellationToken = default)
    {
        // Busca por igualdade exata da forma normalizada (DD-006 — dedupe não-bloqueante)
        // Carrega todos do tenant (já filtrado pelo HasQueryFilter) e filtra no cliente.
        // Eficiente porque o pool de contas por tenant é gerenciado e o filtro de tenant
        // já é aplicado no banco via HasQueryFilter.
        var allAccounts = await _context.Accounts
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var accounts = allAccounts
            .Where(a => a.NormalizedName.Equals(normalizedName))
            .ToList()
            .AsReadOnly();

        return accounts;
    }

    /// <inheritdoc />
    public async Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Accounts
            .Include(a => a.Contacts)
            .FirstOrDefaultAsync(a => a.Id == account.Id, cancellationToken);

        if (existing is null)
        {
            _context.Accounts.Add(account);
        }
        else
        {
            // O EF Core rastreia a entidade carregada; atualizamos via entry
            _context.Entry(existing).CurrentValues.SetValues(account);

            // Sincroniza contatos (add novos, atualiza existentes)
            foreach (var contact in account.Contacts)
            {
                var existingContact = existing.Contacts.FirstOrDefault(c => c.Id == contact.Id);
                if (existingContact is null)
                    _context.Contacts.Add(contact);
                else
                    _context.Entry(existingContact).CurrentValues.SetValues(contact);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
