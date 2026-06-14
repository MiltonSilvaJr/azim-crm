namespace PartnerManagement.Domain.Partners.Repositories;

/// <summary>
/// Interface do repositório de parceiros, definida no Domain sem referência a EF Core ou ORM.
/// Implementada na camada Infrastructure (<see cref="PartnerRepository"/>).
/// Todas as operações são escopadas ao <c>tenant_id</c> do contexto corrente (filtro global EF + RLS, DD-001).
/// Mapeia: Req 1, Req 2, Req 3, Req 4, design §3, design §4.1.
/// </summary>
public interface IPartnerRepository
{
    /// <summary>
    /// Obtém um parceiro pelo identificador.
    /// Retorna <c>null</c> se o parceiro não existir ou não pertencer ao tenant do contexto (anti-enumeração, RNF 1.4).
    /// </summary>
    /// <param name="id">Identificador do parceiro.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<Partner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste um novo parceiro.
    /// </summary>
    /// <param name="partner">Parceiro a ser adicionado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task AddAsync(Partner partner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste as alterações de um parceiro existente.
    /// </summary>
    /// <param name="partner">Parceiro com estado atualizado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task UpdateAsync(Partner partner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca parceiros pelo nome (case-insensitive) dentro do tenant do contexto.
    /// Usado para verificar duplicidade (alerta MSG-021 — não bloqueia a criação, Req 1.7).
    /// </summary>
    /// <param name="name">Nome a ser buscado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de parceiros com nome similar (pode ser vazia).</returns>
    Task<IReadOnlyList<Partner>> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
