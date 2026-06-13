namespace Authentication.Application.Ports.Exceptions;

/// <summary>
/// Exceção lançada quando o envio de e-mail via <see cref="IEmailSender"/> falha.
///
/// O serviço de aplicação captura esta exceção e aplica a regra do caso de uso
/// (ex.: convite criado mas e-mail não enviado → AUTH-ERR-032).
///
/// Mapeia: ADR-0005, design.md § 6.4, Req 7.2.
/// </summary>
public sealed class EmailSenderException : Exception
{
    /// <summary>
    /// Inicializa uma nova instância de <see cref="EmailSenderException"/>.
    /// </summary>
    /// <param name="message">Descrição interna da falha (não exposta ao consumidor).</param>
    public EmailSenderException(string message) : base(message) { }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="EmailSenderException"/> com causa raiz.
    /// </summary>
    /// <param name="message">Descrição interna da falha.</param>
    /// <param name="innerException">Exceção original do adapter de e-mail.</param>
    public EmailSenderException(string message, Exception innerException)
        : base(message, innerException) { }
}
