using Digest.Domain.ValueObjects;
using MediatR;

namespace Digest.Application.Commands;

/// <summary>
/// Comando que dispara o envio do digest para um usuário específico.
/// Disparado por <c>RunDigestForTenantHandler</c> para cada destinatário elegível (design §5.1).
/// O handler garante idempotência via reserva antes do envio (DD-008, Req 9, PBT-02).
/// </summary>
/// <param name="TenantId">Identificador do tenant.</param>
/// <param name="UserId">Identificador do usuário destinatário.</param>
/// <param name="DigestDate">Data local do digest no fuso do tenant.</param>
/// <param name="UserEmail">
/// Endereço de e-mail do destinatário. Nunca logar (DD-011, RNF 3).
/// Obrigatório para o envio via <c>IEmailSender</c>.
/// </param>
/// <param name="CorrelationId">Identificador de correlação para rastreabilidade.</param>
public sealed record SendUserDigestCommand(
    Guid TenantId,
    Guid UserId,
    DigestDate DigestDate,
    string UserEmail,
    Guid? CorrelationId = null) : IRequest<SendUserDigestResult>;

/// <summary>
/// Resultado do processamento de <see cref="SendUserDigestCommand"/>.
/// </summary>
/// <param name="Sent">Indica se o e-mail foi enviado nesta execução.</param>
/// <param name="Skipped">Indica se o envio foi ignorado por idempotência (já enviado).</param>
/// <param name="MessageId">Identificador da mensagem no provedor, quando enviado.</param>
public sealed record SendUserDigestResult(bool Sent, bool Skipped, string? MessageId = null);
