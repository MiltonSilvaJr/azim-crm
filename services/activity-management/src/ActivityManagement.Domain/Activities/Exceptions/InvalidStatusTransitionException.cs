namespace ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Exceção lançada quando a transição de status solicitada não é permitida
/// pela máquina de estados de <see cref="ActivityManagement.Domain.Activities.ValueObjects.ActivityStatus"/>.
/// Protege o invariante I4 (design §4.1, Req 4.2, ACT-ERR-004).
/// </summary>
public sealed class InvalidStatusTransitionException : ActivityDomainException
{
    /// <summary>
    /// Inicializa a exceção com os status de origem e destino da transição inválida.
    /// </summary>
    /// <param name="fromStatus">Status atual da atividade.</param>
    /// <param name="toStatus">Status de destino solicitado (inválido).</param>
    public InvalidStatusTransitionException(string fromStatus, string toStatus)
        : base($"Transição de status inválida: '{fromStatus}' → '{toStatus}' não é permitida pela máquina de estados.")
    {
        FromStatus = fromStatus;
        ToStatus   = toStatus;
    }

    /// <summary>Status atual da atividade no momento da tentativa.</summary>
    public string FromStatus { get; }

    /// <summary>Status de destino solicitado (inválido).</summary>
    public string ToStatus { get; }
}
