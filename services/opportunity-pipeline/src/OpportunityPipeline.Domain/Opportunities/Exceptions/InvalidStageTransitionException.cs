using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// Transição de estágio inválida pela máquina de estados (design §4.5).
/// Lançada ANTES de qualquer mutação — aggregate permanece inalterado.
/// Mapeia: Req 6.5, OP-ERR-013, PBT-08.
/// </summary>
public sealed class InvalidStageTransitionException : DomainException
{
    /// <summary>Cria exceção com contexto de origem e destino.</summary>
    public InvalidStageTransitionException(StageCategory from, StageCategory to)
        : base(
            $"Transição de estágio inválida: {from} → {to}. " +
            "Transições permitidas: open→open, open→won, open→lost, won→open, lost→open (OP-ERR-013).")
    {
    }
}
