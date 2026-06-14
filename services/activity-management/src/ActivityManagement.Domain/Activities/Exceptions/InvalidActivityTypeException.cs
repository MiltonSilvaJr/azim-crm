namespace ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Exceção lançada quando o tipo da atividade não pertence à lista canônica
/// {meeting, follow_up, call, email, task}.
/// Mapeia: design §4.3, Req 1.6, ACT-ERR-002, invariante I2.
/// </summary>
public sealed class InvalidActivityTypeException : ArgumentException
{
    /// <summary>
    /// Inicializa a exceção com o valor inválido recebido.
    /// </summary>
    /// <param name="value">Valor de tipo inválido que originou a exceção.</param>
    public InvalidActivityTypeException(string value)
        : base($"Tipo de atividade inválido: '{value}'. Valores aceitos: meeting, follow_up, call, email, task.", nameof(value))
    {
        InvalidValue = value;
    }

    /// <summary>Valor inválido que originou a exceção.</summary>
    public string InvalidValue { get; }
}
