namespace ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Exceção lançada quando o título da atividade está em branco ou nulo.
/// Protege o invariante I1 do agregado Activity (design §4.1, Req 1.5, ACT-ERR-001).
/// </summary>
public sealed class TitleRequiredException : ActivityDomainException
{
    /// <summary>Inicializa a exceção com mensagem padrão.</summary>
    public TitleRequiredException()
        : base("Título da atividade é obrigatório e não pode estar em branco.")
    {
    }
}
