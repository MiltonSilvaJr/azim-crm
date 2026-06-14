using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Services;

/// <summary>
/// Máquina de estados do ciclo de vida da oportunidade.
/// Valida transições ANTES de qualquer mutação — aggregate permanece inalterado em caso de rejeição.
/// Transições válidas (design §4.5):
///   (none) → open  : Create
///   open → open    : MoveStage (estágio diferente, mesma categoria)
///   open → won     : Win (snapshot de comissão)
///   open → lost    : Lose (loss_reason obrigatório)
///   won  → open    : Reopen (RBAC: TenantAdmin/GestorBU)
///   lost → open    : Reopen (RBAC: TenantAdmin/GestorBU)
/// Mapeia: Req 6, Req 6.5, PBT-08, design §4.5.
/// </summary>
public static class OpportunityLifecycle
{
    /// <summary>
    /// Valida se a transição de categoria é permitida.
    /// Lança InvalidStageTransitionException ANTES de qualquer mutação (PBT-08).
    /// </summary>
    /// <exception cref="InvalidStageTransitionException">Se a transição for inválida.</exception>
    public static void ValidateTransition(StageCategory from, StageCategory to)
    {
        var isValid = (from, to) switch
        {
            (StageCategory.Open, StageCategory.Open) => true,
            (StageCategory.Open, StageCategory.Won) => true,
            (StageCategory.Open, StageCategory.Lost) => true,
            (StageCategory.Won, StageCategory.Open) => true,
            (StageCategory.Lost, StageCategory.Open) => true,
            _ => false
        };

        if (!isValid)
            throw new InvalidStageTransitionException(from, to);
    }

    /// <summary>
    /// Verifica se uma categoria é terminal (won ou lost).
    /// Estados terminais não permitem MoveStage (apenas Reopen os reabre).
    /// </summary>
    public static bool IsTerminal(StageCategory category) =>
        category is StageCategory.Won or StageCategory.Lost;
}
