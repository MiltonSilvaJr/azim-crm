namespace ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Exceção lançada quando se tenta realizar uma operação de escrita em uma atividade
/// que se encontra em estado terminal (completed ou cancelled).
/// Protege o invariante I6 (design §4.1, Req 2.1, Req 4.3, Req 8.1, ACT-ERR-011).
/// </summary>
public sealed class ActivityTerminalException : ActivityDomainException
{
    /// <summary>
    /// Inicializa a exceção com o status terminal atual da atividade.
    /// </summary>
    /// <param name="terminalStatus">Status terminal que bloqueou a operação.</param>
    public ActivityTerminalException(string terminalStatus)
        : base($"Operação inválida: a atividade encontra-se no estado terminal '{terminalStatus}' e não aceita modificações.")
    {
        TerminalStatus = terminalStatus;
    }

    /// <summary>Status terminal que originou a exceção.</summary>
    public string TerminalStatus { get; }
}
