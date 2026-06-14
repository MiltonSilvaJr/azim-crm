namespace Digest.Domain.Enums;

/// <summary>
/// Status canônico do ciclo de vida de <see cref="Entities.EmailDigestLog"/>.
/// Mapeado na coluna <c>status</c> de <c>email_digest_logs</c> (design §4.5, design §7).
/// </summary>
public enum DigestStatus
{
    /// <summary>
    /// Reserva de idempotência inserida antes de chamar <c>IEmailSender</c>.
    /// Garante que apenas um worker processa o envio (DD-008).
    /// </summary>
    Scheduled,

    /// <summary>Provedor de e-mail aceitou a mensagem; <c>message_id</c> registrado.</summary>
    Sent,

    /// <summary>Provedor confirmou entrega na caixa do destinatário.</summary>
    Delivered,

    /// <summary>Destinatário abriu o e-mail (evento do provedor). Terminal.</summary>
    Opened,

    /// <summary>Provedor reportou bounce (rejeição permanente ou temporária). Terminal.</summary>
    Bounced,

    /// <summary>Falha definitiva após esgotar retentativas. Terminal (RNF 5.2).</summary>
    Failed,
}
