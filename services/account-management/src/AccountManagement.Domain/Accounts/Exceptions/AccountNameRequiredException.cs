namespace AccountManagement.Domain.Accounts.Exceptions;

/// <summary>
/// Exceção lançada quando o nome da conta ou do contato é nulo, vazio ou excede
/// o comprimento máximo permitido.
///
/// Mapeia: design §4.1 invariante I1, design §4.3, Req 1.5, Req 5.2, ACC-ERR-001, ACC-ERR-005.
/// </summary>
public sealed class AccountNameRequiredException : Exception
{
    /// <summary>Inicializa a exceção com a mensagem padrão.</summary>
    public AccountNameRequiredException()
        : base("O nome é obrigatório e não pode ser vazio ou ultrapassar o comprimento máximo.")
    {
    }

    /// <summary>Inicializa a exceção com uma mensagem personalizada.</summary>
    /// <param name="message">Mensagem descritiva do erro.</param>
    public AccountNameRequiredException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Inicializa a exceção com mensagem e exceção interna.
    /// </summary>
    /// <param name="message">Mensagem descritiva do erro.</param>
    /// <param name="innerException">Exceção que causou este erro.</param>
    public AccountNameRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
