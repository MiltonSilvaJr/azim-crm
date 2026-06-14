namespace PartnerManagement.Application.Ports;

/// <summary>
/// Porta de leitura do read model <c>opportunity_partner_commissions</c> do opportunity-pipeline.
/// Declarada no Application; implementada na Infrastructure (<c>PartnerCommissionReadAdapter</c>).
/// Este módulo nunca recalcula nem persiste comissão — apenas consome o read model (DD-003, P3).
/// Em indisponibilidade do read port, os handlers aplicam degradação parcial (design §5.3, design §15).
/// Mapeia: Req 9, Req 10, PBT-01, PBT-05, DD-003, design §5.2, design §6.4.
/// </summary>
public interface IPartnerCommissionReadPort
{
    /// <summary>
    /// Obtém as linhas de comissão de um parceiro dentro de um período.
    /// Cada linha representa uma oportunidade com comissão projetada ou consolidada (snapshot).
    /// </summary>
    /// <param name="tenantId">Tenant do contexto.</param>
    /// <param name="partnerId">Identificador do parceiro.</param>
    /// <param name="from">Início do período (UTC).</param>
    /// <param name="to">Fim do período (UTC).</param>
    /// <param name="correlationId">Identificador de correlação propagado ao pipeline.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Linhas de comissão do parceiro no período.</returns>
    Task<IReadOnlyList<CommissionLine>> GetCommissionLinesAsync(
        Guid tenantId,
        Guid partnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Linha de comissão de um parceiro em uma oportunidade.
/// Pertence ao read model do opportunity-pipeline; este módulo apenas consome (DD-003).
/// </summary>
/// <param name="OpportunityId">Identificador da oportunidade.</param>
/// <param name="CommissionCents">Valor da comissão em centavos inteiros (RNF 6, money-as-cents).</param>
/// <param name="IsSnapshot">
/// <c>true</c> = comissão consolidada (snapshot de oportunidade ganha);
/// <c>false</c> = comissão projetada (oportunidade aberta).
/// </param>
/// <param name="OccurredAt">Data de registro da comissão no pipeline.</param>
public sealed record CommissionLine(
    Guid OpportunityId,
    long CommissionCents,
    bool IsSnapshot,
    DateTimeOffset OccurredAt
);
