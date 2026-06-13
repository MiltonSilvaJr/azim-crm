using Authentication.Application.Ports.Results;

namespace Authentication.Application.Services;

/// <summary>
/// Exceção lançada quando o convite foi criado no IdP mas o envio do e-mail falhou.
///
/// Carrega o <see cref="ActivationLink"/> gerado para que o caller possa logar
/// ou oferecer reenvio sem precisar recriar o convite no IdP (Req 7.2, AUTH-ERR-032).
///
/// O convite não é revertido: o link gerado permanece válido no IdP.
///
/// Mapeia: Req 7.2, design.md § 8.3, AUTH-ERR-032.
/// </summary>
public sealed class InviteEmailFailedException : Exception
{
    /// <summary>Código de erro do catálogo (AUTH-ERR-032).</summary>
    public string ErrorCode { get; }

    /// <summary>Link de ativação gerado (convite criado com sucesso no IdP).</summary>
    public ActivationLinkResult ActivationLink { get; }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="InviteEmailFailedException"/>.
    /// </summary>
    /// <param name="activationLink">Link gerado antes da falha de e-mail.</param>
    /// <param name="innerException">Exceção original do EmailSender.</param>
    public InviteEmailFailedException(
        ActivationLinkResult activationLink,
        Exception innerException)
        : base("Convite criado mas e-mail não pôde ser enviado (AUTH-ERR-032).", innerException)
    {
        ErrorCode = "AUTH-ERR-032";
        ActivationLink = activationLink;
    }
}
