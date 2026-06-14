using Digest.Domain.ValueObjects;

namespace Digest.Domain.Events;

/// <summary>
/// Domain event registrado após envio bem-sucedido do digest para um usuário.
/// Publicado via Outbox como <c>digest.email_sent.v1</c> para o audit-log (design §4.4, DD-009).
/// Sem PII: não contém e-mail, nome do usuário nem conteúdo do digest (RNF 3.4, RNF 10.2).
/// </summary>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="UserId">Identificador do usuário destinatário (opaco — sem e-mail).</param>
/// <param name="DigestDate">Data local do digest no fuso do tenant.</param>
/// <param name="MessageId">Identificador da mensagem no provedor de e-mail (sem PII).</param>
/// <param name="OccurredAt">Timestamp UTC do evento.</param>
public sealed record DigestEmailSent(
    Guid TenantId,
    Guid UserId,
    DigestDate DigestDate,
    string MessageId,
    DateTimeOffset OccurredAt);
