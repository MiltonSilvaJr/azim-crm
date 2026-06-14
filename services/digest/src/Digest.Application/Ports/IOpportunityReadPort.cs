using Digest.Application.Models;

namespace Digest.Application.Ports;

/// <summary>
/// Porta de leitura somente-leitura de oportunidades estagnadas, fechamentos vencidos e pipeline ponderado.
/// Fonte: módulo opportunity-pipeline (design §6.4, Req 4, DD-001).
/// Implementação concreta vive em <c>Digest.Infrastructure</c>.
/// </summary>
/// <remarks>
/// Restrições:
/// <list type="bullet">
///   <item>Toda consulta é restrita ao <c>tenant_id</c> (Req 6.4).</item>
///   <item>Nunca lança exceção por dado faltante — retorna lista vazia.</item>
///   <item>Critério de estagnação (RN-028) calculado pelo módulo dono — o digest apenas lê o resultado.</item>
/// </list>
/// </remarks>
public interface IOpportunityReadPort
{
    /// <summary>
    /// Retorna as oportunidades estagnadas do proprietário (critério RN-028, calculado pelo opportunity-pipeline).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="ownerId">Identificador do proprietário (user_id). Obrigatório.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de oportunidades estagnadas; nunca <see langword="null"/>.</returns>
    Task<IReadOnlyList<OpportunityItem>> GetStaleOpportunitiesAsync(
        Guid tenantId,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as oportunidades com fechamento previsto vencido (data &lt; <paramref name="referenceDate"/>).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="ownerId">Identificador do proprietário (user_id). Obrigatório.</param>
    /// <param name="referenceDate">Data de referência local do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de oportunidades com fechamento vencido; nunca <see langword="null"/>.</returns>
    Task<IReadOnlyList<OpportunityItem>> GetOverdueClosingsAsync(
        Guid tenantId,
        Guid ownerId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o pipeline ponderado consolidado do tenant para composição do azimute (Req 5.2–5.3).
    /// Usado apenas na segunda-feira por usuários de gestão.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de itens do pipeline ponderado; nunca <see langword="null"/>.</returns>
    Task<IReadOnlyList<OpportunityItem>> GetWeightedPipelineAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
