using DataMigration.Domain.ValueObjects;

namespace DataMigration.Domain.Policies;

/// <summary>
/// Resultado da aplicação da <see cref="PartnerPctPendingPolicy"/>.
/// </summary>
/// <param name="Flag">Flag gerada (não-bloqueante) ou <c>null</c>.</param>
public sealed record PartnerPctPendingResult(TriageFlag? Flag);

/// <summary>
/// Policy que gera flag não-bloqueante quando um parceiro não tem percentual informado.
///
/// Percentuais não são importados pelo módulo (design §6.4, Req 3.4).
/// A falta de percentual não bloqueia o import (Req 5.2).
///
/// Rastreia: design §4.6, Req 3.4, 5.2, TASK-07.
/// </summary>
public sealed class PartnerPctPendingPolicy
{
    /// <summary>
    /// Aplica a policy de percentual de parceiro.
    /// </summary>
    /// <param name="partnerName">Nome do parceiro (informativo).</param>
    /// <param name="pctProvided">Se o percentual foi fornecido na planilha.</param>
    /// <param name="sourceRowIndex">Índice da linha para referência do flag.</param>
    public PartnerPctPendingResult Apply(
        string? partnerName,
        bool pctProvided,
        int sourceRowIndex = 0)
    {
        if (!pctProvided && !string.IsNullOrWhiteSpace(partnerName))
        {
            var flag = TriageFlag.ForRow(TriageFlagType.PartnerPctMissing, sourceRowIndex);
            return new PartnerPctPendingResult(flag);
        }

        return new PartnerPctPendingResult(Flag: null);
    }
}
