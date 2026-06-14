namespace ActivityManagement.Domain.Activities.Exceptions;

/// <summary>
/// Classe base para todas as exceções de domínio do módulo activity-management.
/// Herdar desta classe permite capturar erros de domínio de forma tipada nos handlers
/// da camada Application sem vazar detalhes de infraestrutura.
/// Mapeia: design §4 (invariantes I1–I6), TASK-03.
/// </summary>
public abstract class ActivityDomainException : Exception
{
    /// <summary>Inicializa a exceção com uma mensagem descritiva.</summary>
    /// <param name="message">Mensagem de erro não vazia.</param>
    protected ActivityDomainException(string message)
        : base(message)
    {
    }

    /// <summary>Inicializa a exceção com uma mensagem e exceção interna.</summary>
    /// <param name="message">Mensagem de erro não vazia.</param>
    /// <param name="inner">Exceção interna que originou esta.</param>
    protected ActivityDomainException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
