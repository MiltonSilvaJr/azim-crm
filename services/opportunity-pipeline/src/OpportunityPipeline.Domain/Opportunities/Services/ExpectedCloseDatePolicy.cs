namespace OpportunityPipeline.Domain.Opportunities.Services;

/// <summary>
/// Política de domínio pura: decide se expected_close_date é obrigatória.
/// Obrigatória quando stage.order ≥ order("Proposta Enviada") (Req 9, RN-003).
/// Sem dependências de infraestrutura. 100% determinístico.
/// Mapeia: Req 9, INV-6, design §4.6.
/// </summary>
public static class ExpectedCloseDatePolicy
{
    /// <summary>
    /// Verifica se a data de fechamento esperada é obrigatória para o estágio.
    /// </summary>
    /// <param name="stageOrder">Ordem do estágio destino.</param>
    /// <param name="propostaEnviadaOrder">Ordem do estágio "Proposta Enviada" configurado.</param>
    /// <returns>True se a data for obrigatória (OP-ERR-005).</returns>
    public static bool IsRequired(int stageOrder, int propostaEnviadaOrder)
    {
        return stageOrder >= propostaEnviadaOrder;
    }
}
