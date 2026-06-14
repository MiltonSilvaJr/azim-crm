namespace AccountManagement.Application.Exceptions;

/// <summary>
/// Exceção lançada quando uma conta não é encontrada ou não pertence ao tenant do contexto.
///
/// Corresponde ao erro ACC-ERR-003 (design §12).
/// Não distingue "não existe" de "fora do tenant" para evitar enumeração (Req 9, PBT-04).
///
/// Mapeia: design §12, Req 10, ACC-ERR-003.
/// </summary>
public sealed class AccountNotFoundException : Exception
{
    /// <summary>Inicializa com a mensagem padrão do catálogo de erros.</summary>
    public AccountNotFoundException()
        : base("Conta não encontrada.")
    {
    }

    /// <summary>Inicializa com mensagem personalizada.</summary>
    /// <param name="message">Mensagem descritiva.</param>
    public AccountNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Inicializa com mensagem e exceção interna.</summary>
    public AccountNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
