namespace NotificationDelivery.Contracts;

/// <summary>
/// Motivo canônico de falha no envio de e-mail.
///
/// Value object imutável com igualdade por valor (record).
/// Construído apenas com <see cref="Code"/> do catálogo <see cref="FailureCode"/>,
/// uma mensagem legível sem PII e a flag de retriabilidade.
///
/// Invariantes (design §4.3, Req 3):
/// <list type="bullet">
///   <item><description><see cref="Code"/> não pode ser nulo, vazio ou apenas espaços — lança <see cref="ArgumentException"/> na construção.</description></item>
///   <item><description><see cref="Message"/> nunca expõe o e-mail do destinatário nem credencial (RNF 4, Req 3.3).</description></item>
/// </list>
/// </summary>
/// <param name="Code">Código do catálogo de erros (<see cref="FailureCode"/>).</param>
/// <param name="Message">Mensagem legível sem PII, estável e classificável.</param>
/// <param name="IsRetriable">Indica se o chamador pode re-tentar após esta falha.</param>
public sealed record FailureReason(string Code, string Message, bool IsRetriable)
{
    /// <summary>
    /// Código do catálogo de erros.
    /// Use as constantes de <see cref="FailureCode"/> para evitar strings mágicas.
    /// </summary>
    public string Code { get; } = ValidateCode(Code);

    /// <summary>
    /// Mensagem legível, estável e sem PII (e-mail, credencial).
    /// </summary>
    public string Message { get; } = Message ?? string.Empty;

    /// <summary>
    /// Indica se o chamador pode re-tentar.
    /// <see cref="SendStatus.PermanentFailure"/> implica <c>false</c>;
    /// <see cref="SendStatus.TransientFailure"/> implica <c>true</c>.
    /// </summary>
    public bool IsRetriable { get; } = IsRetriable;

    // -------------------------------------------------------------------------
    // Validação de invariante
    // -------------------------------------------------------------------------

    private static string ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "O código de falha não pode ser nulo, vazio ou apenas espaços em branco.",
                nameof(code));

        return code;
    }
}
