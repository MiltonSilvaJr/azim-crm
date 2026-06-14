using Digest.Application.Repositories;
using Digest.Domain.Enums;
using MediatR;

namespace Digest.Application.Commands;

/// <summary>
/// Handler de <see cref="UpdateDeliveryStatusCommand"/>.
/// Mapeia eventos de entrega do provedor para a state machine de <see cref="Domain.Entities.EmailDigestLog"/>.
/// Idempotente por <c>message_id</c> — processamentos duplicados não geram transição inválida (design §5.3).
/// </summary>
public sealed class UpdateDeliveryStatusHandler
    : IRequestHandler<UpdateDeliveryStatusCommand, UpdateDeliveryStatusResult>
{
    private readonly IEmailDigestLogRepository _logRepository;

    /// <summary>
    /// Constrói o handler.
    /// </summary>
    public UpdateDeliveryStatusHandler(IEmailDigestLogRepository logRepository)
    {
        _logRepository = logRepository;
    }

    /// <inheritdoc/>
    public async Task<UpdateDeliveryStatusResult> Handle(
        UpdateDeliveryStatusCommand request,
        CancellationToken cancellationToken)
    {
        // Valida que o status é um status de entrega aceitável pelo handler
        if (request.ProviderStatus is not (DigestStatus.Delivered or DigestStatus.Opened or DigestStatus.Bounced))
        {
            // Status inválido para este handler — ignora (não lança exceção)
            return new UpdateDeliveryStatusResult(Updated: false, Skipped: true);
        }

        try
        {
            await _logRepository.UpdateStatusByMessageIdAsync(
                request.MessageId,
                request.ProviderStatus,
                cancellationToken);

            return new UpdateDeliveryStatusResult(Updated: true, Skipped: false);
        }
        catch (InvalidOperationException)
        {
            // Transição inválida na state machine (ex.: já no terminal) — idempotente
            return new UpdateDeliveryStatusResult(Updated: false, Skipped: true);
        }
    }
}
