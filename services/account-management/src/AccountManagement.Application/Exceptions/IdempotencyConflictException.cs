namespace AccountManagement.Application.Exceptions;

/// <summary>
/// Exceção lançada quando uma <c>Idempotency-Key</c> é reutilizada com payload divergente.
///
/// Corresponde ao erro ACC-ERR-009 (design §12).
/// O cliente deve usar nova chave ou reenviar o payload idêntico ao original.
///
/// Mapeia: design §12, design §6.5, ACC-ERR-009, TASK-13.
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    /// <summary>Inicializa com a mensagem padrão do catálogo de erros.</summary>
    public IdempotencyConflictException()
        : base("Requisição duplicada com payload divergente.")
    {
    }

    /// <summary>Inicializa com mensagem personalizada.</summary>
    public IdempotencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Inicializa com mensagem e exceção interna.</summary>
    public IdempotencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
