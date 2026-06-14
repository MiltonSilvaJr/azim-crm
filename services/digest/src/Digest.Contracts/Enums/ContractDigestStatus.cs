namespace Digest.Contracts.Enums;

/// <summary>
/// Status canônico do ciclo de vida de um envio de digest por e-mail (design §4.5, requirements §4.2).
/// Publicado em eventos e usado em contratos externos (ex.: webhook de status de entrega).
/// Espelha <c>Digest.Domain.Enums.DigestStatus</c> sem depender do projeto de domínio (Contracts → ∅).
/// </summary>
public enum ContractDigestStatus
{
    /// <summary>
    /// Reserva de idempotência inserida antes do envio (DD-008).
    /// Barreira de corrida via UNIQUE (tenant_id, user_id, digest_date).
    /// </summary>
    Scheduled,

    /// <summary>Provedor de e-mail aceitou a mensagem; message_id registrado.</summary>
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
