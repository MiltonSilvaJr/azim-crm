namespace ActivityManagement.Domain.Activities.Specifications;

/// <summary>
/// Specification que determina se uma atividade está vencida.
/// Regra: atividade é vencida ⟺ <c>dueAt &lt; referenceInstant</c> E status não terminal.
/// Atividades terminais (<c>completed</c> ou <c>cancelled</c>) NUNCA são vencidas,
/// independentemente da <c>dueAt</c>.
/// O critério de "vencida" usa o instante absoluto (UTC), não o fuso do tenant —
/// conforme Req 11.1 e DD-008.
/// Mapeia: design §4.6, Req 11.1, Req 5.1, PBT-04, TASK-05.
/// </summary>
public sealed class OverdueSpecification
{
    private readonly DateTimeOffset _referenceInstant;

    /// <summary>
    /// Inicializa a specification com o instante de referência.
    /// </summary>
    /// <param name="referenceInstant">Instante de comparação (tipicamente <c>now</c> em UTC).</param>
    public OverdueSpecification(DateTimeOffset referenceInstant)
    {
        _referenceInstant = referenceInstant;
    }

    /// <summary>
    /// Avalia se a atividade está vencida conforme a regra do domínio.
    /// </summary>
    /// <param name="activity">Atividade a avaliar.</param>
    /// <returns>
    /// <c>true</c> quando <c>dueAt &lt; referenceInstant</c> e o status não é terminal;
    /// <c>false</c> em qualquer outro caso (inclusive para atividades terminais).
    /// </returns>
    public bool IsSatisfiedBy(Activity activity)
    {
        // Terminais nunca são vencidas (PBT-04, Req 11.1)
        if (activity.Status.IsTerminal)
            return false;

        // dueAt < referenceInstant (estritamente menor — igual não é vencida)
        return activity.DueAt.Value < _referenceInstant;
    }
}
