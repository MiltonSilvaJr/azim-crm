using Digest.Application.Models;
using Digest.Domain.ValueObjects;

namespace Digest.Application.Abstractions;

/// <summary>
/// Abstração da porta de envio de e-mail.
/// Implementação concreta é o adaptador ACL para o módulo notification-delivery (design §3, Req 8).
/// Implementação concreta vive em <c>Digest.Infrastructure</c>.
/// </summary>
/// <remarks>
/// Sem PII nos parâmetros de log: o endereço de e-mail do destinatário é parte do <see cref="SendRequest"/>
/// mas nunca deve ser logado (RNF 3, DD-011).
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Envia o digest ao destinatário.
    /// </summary>
    /// <param name="request">Dados do envio, incluindo endereço de e-mail e conteúdo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado do envio com o <c>message_id</c> do provedor (sem PII).</returns>
    Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dados necessários para envio do digest por e-mail.
/// </summary>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="UserId">Identificador do usuário destinatário.</param>
/// <param name="ToEmail">Endereço de e-mail do destinatário. Nunca logar (DD-011).</param>
/// <param name="DigestDate">Data do digest.</param>
/// <param name="Content">Conteúdo composto do digest.</param>
/// <param name="CorrelationId">Identificador de correlação para rastreabilidade (sem PII).</param>
public sealed record SendRequest(
    Guid TenantId,
    Guid UserId,
    string ToEmail,
    DigestDate DigestDate,
    DigestContent Content,
    Guid? CorrelationId = null);

/// <summary>
/// Resultado do envio de e-mail.
/// </summary>
/// <param name="MessageId">Identificador da mensagem no provedor (sem PII). Nunca nulo em sucesso.</param>
/// <param name="Success">Indica se o provedor aceitou a mensagem.</param>
public sealed record SendResult(string MessageId, bool Success);
