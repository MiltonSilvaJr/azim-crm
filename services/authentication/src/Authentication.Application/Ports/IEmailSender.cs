namespace Authentication.Application.Ports;

/// <summary>
/// Porta de saída que abstrai o envio de e-mails transacionais via o módulo
/// <c>notification-delivery</c>.
///
/// Falha de envio resulta em <see cref="EmailSenderException"/> (ADR-0005).
/// O serviço de aplicação trata a falha conforme a regra do caso de uso:
///   - Convite: falha → AUTH-ERR-032, convite NÃO é revertido (Req 7.2).
///   - Reset: resposta uniforme independente do resultado (anti-enumeração, PBT-03).
///
/// Mapeia: ADR-0005, design.md § 6.4, Req 7.2, Req 8.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envia um e-mail de ativação de convite ao destinatário.
    /// </summary>
    /// <param name="recipientEmail">E-mail do convidado.</param>
    /// <param name="activationUrl">URL de ativação gerada pelo IdP.</param>
    /// <param name="tenantId">UUID do tenant (para rastreabilidade e particionamento).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <exception cref="EmailSenderException">Lançada quando o envio falha.</exception>
    Task SendInviteEmailAsync(
        string recipientEmail,
        string activationUrl,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia um e-mail de redefinição de senha ao destinatário.
    /// </summary>
    /// <param name="recipientEmail">E-mail do usuário.</param>
    /// <param name="resetUrl">URL de redefinição gerada pelo IdP.</param>
    /// <param name="tenantId">UUID do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <exception cref="EmailSenderException">Lançada quando o envio falha.</exception>
    Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string resetUrl,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
