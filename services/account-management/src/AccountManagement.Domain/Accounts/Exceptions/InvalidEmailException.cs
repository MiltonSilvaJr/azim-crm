namespace AccountManagement.Domain.Accounts.Exceptions;

/// <summary>
/// Exceção lançada quando o endereço de e-mail de um contato está em formato inválido.
///
/// Mapeia: design §4.3, Req 5.4, ACC-ERR-004.
/// </summary>
public sealed class InvalidEmailException : Exception
{
    /// <summary>Inicializa a exceção com a mensagem padrão.</summary>
    public InvalidEmailException()
        : base("O endereço de e-mail é inválido.")
    {
    }

    /// <summary>Inicializa a exceção com mensagem personalizada.</summary>
    /// <param name="message">Mensagem descritiva do erro.</param>
    public InvalidEmailException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Inicializa a exceção com mensagem e exceção interna.
    /// </summary>
    /// <param name="message">Mensagem descritiva do erro.</param>
    /// <param name="innerException">Exceção que causou este erro.</param>
    public InvalidEmailException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
