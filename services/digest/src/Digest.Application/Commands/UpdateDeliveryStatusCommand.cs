using Digest.Domain.Enums;
using MediatR;

namespace Digest.Application.Commands;

/// <summary>
/// Comando que atualiza o status de entrega de um e-mail a partir de evento do provedor.
/// Disparado pelo webhook de entrega hospedado por notification-delivery (Req 11.1, design §5.1).
/// Idempotente por <see cref="MessageId"/> (design §5.3).
/// </summary>
/// <param name="MessageId">Identificador da mensagem no provedor (sem PII).</param>
/// <param name="ProviderStatus">Novo status reportado pelo provedor.</param>
public sealed record UpdateDeliveryStatusCommand(
    string MessageId,
    DigestStatus ProviderStatus) : IRequest<UpdateDeliveryStatusResult>;

/// <summary>
/// Resultado do processamento de <see cref="UpdateDeliveryStatusCommand"/>.
/// </summary>
/// <param name="Updated">Indica se o status foi atualizado nesta execução.</param>
/// <param name="Skipped">Indica se o status não foi atualizado (já no status terminal ou não encontrado).</param>
public sealed record UpdateDeliveryStatusResult(bool Updated, bool Skipped);
