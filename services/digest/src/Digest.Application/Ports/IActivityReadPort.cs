using Digest.Application.Models;

namespace Digest.Application.Ports;

/// <summary>
/// Porta de leitura somente-leitura de atividades vencidas e do dia por proprietário.
/// Fonte: módulo activity-management (design §6.4, Req 4, DD-001).
/// Implementação concreta vive em <c>Digest.Infrastructure</c> (adaptador HTTP ou DB).
/// </summary>
/// <remarks>
/// Restrições:
/// <list type="bullet">
///   <item>Toda consulta é restrita ao <c>tenant_id</c> (Req 6.4) — never cross-tenant.</item>
///   <item>Nunca lança exceção por dado faltante — retorna lista vazia.</item>
///   <item>Sem escrita no schema do activity-management (Req 6.2).</item>
/// </list>
/// </remarks>
public interface IActivityReadPort
{
    /// <summary>
    /// Retorna as atividades vencidas (data de vencimento &lt; <paramref name="referenceDate"/>) do proprietário.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="ownerId">Identificador do proprietário (user_id). Obrigatório.</param>
    /// <param name="referenceDate">Data de referência local do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de atividades vencidas; nunca <see langword="null"/>.</returns>
    Task<IReadOnlyList<ActivityItem>> GetOverdueActivitiesAsync(
        Guid tenantId,
        Guid ownerId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna as atividades previstas para hoje (data de vencimento = <paramref name="referenceDate"/>).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="ownerId">Identificador do proprietário (user_id). Obrigatório.</param>
    /// <param name="referenceDate">Data de referência local do tenant (hoje no fuso do tenant).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de atividades do dia; nunca <see langword="null"/>.</returns>
    Task<IReadOnlyList<ActivityItem>> GetTodayActivitiesAsync(
        Guid tenantId,
        Guid ownerId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default);
}
