namespace AccountManagement.Domain.Accounts.Exceptions;

/// <summary>
/// Exceção lançada quando uma operação de esquecimento (LGPD) é solicitada
/// sobre um contato cujo estado de privacidade já é <c>Anonymized</c>.
///
/// Mapeia: design §4.5, Req 7.5, DD-001, ACC-ERR-007.
/// </summary>
public sealed class ContactAlreadyForgottenException : Exception
{
    /// <summary>Inicializa a exceção com a mensagem padrão.</summary>
    public ContactAlreadyForgottenException()
        : base("O contato já foi anonimizado (direito ao esquecimento já efetivado).")
    {
    }

    /// <summary>Inicializa a exceção com mensagem personalizada.</summary>
    /// <param name="message">Mensagem descritiva do erro.</param>
    public ContactAlreadyForgottenException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Inicializa a exceção com mensagem e exceção interna.
    /// </summary>
    /// <param name="message">Mensagem descritiva do erro.</param>
    /// <param name="innerException">Exceção que causou este erro.</param>
    public ContactAlreadyForgottenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
