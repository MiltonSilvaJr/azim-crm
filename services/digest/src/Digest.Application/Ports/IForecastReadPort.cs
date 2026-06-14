using Digest.Application.Models;

namespace Digest.Application.Ports;

/// <summary>
/// Porta de leitura somente-leitura do bloco de metas (realizado vs. meta do período).
/// Fonte: módulo goal-forecast (design §6.4, Req 5.3–5.4, DD-010).
/// Implementação concreta vive em <c>Digest.Infrastructure</c>.
/// </summary>
/// <remarks>
/// Restrições:
/// <list type="bullet">
///   <item>Toda consulta é restrita ao <c>tenant_id</c> (Req 6.4).</item>
///   <item>
///     Retorna <see langword="null"/> quando não há meta cadastrada para o período —
///     <b>nunca</b> lança exceção por ausência de meta (Req 5.4, PBT-04).
///   </item>
///   <item>Todos os valores monetários retornados em centavos (DD-010).</item>
/// </list>
/// </remarks>
public interface IForecastReadPort
{
    /// <summary>
    /// Retorna o bloco de metas consolidado do tenant para o mês do <paramref name="referenceDate"/>.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Obrigatório.</param>
    /// <param name="referenceDate">Data de referência local do tenant (o mês é extraído desta data).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// <see cref="ForecastBlock"/> quando existe meta para o período;
    /// <see langword="null"/> quando não existe (degradação graciosa — Req 5.4).
    /// </returns>
    Task<ForecastBlock?> GetForecastBlockAsync(
        Guid tenantId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default);
}
