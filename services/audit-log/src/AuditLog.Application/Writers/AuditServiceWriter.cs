using AuditLog.Application.Commands;
using AuditLog.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;
using DomainAction = AuditLog.Domain.ValueObjects.AuditAction;

namespace AuditLog.Application.Writers;

/// <summary>
/// Implementação concreta de <see cref="IAuditWriter"/> para uso in-process (DD-006).
/// Traduz <see cref="AuditEntryRequest"/> (contrato público) em <see cref="RecordAuditEntryCommand"/>
/// (comando interno) e o despacha via MediatR para o <c>AuditService</c>.
/// <para>
/// <b>Nota de segurança:</b> <c>TenantId</c> e <c>CreatedAt</c> nunca vêm do <see cref="AuditEntryRequest"/>;
/// são resolvidos pelo <c>AuditService</c> a partir do contexto autenticado e do relógio do servidor (REQ-005.3, REQ-002.4).
/// </para>
/// </summary>
public sealed class AuditServiceWriter : IAuditWriter
{
    private readonly ISender _sender;
    private readonly ILogger<AuditServiceWriter> _logger;

    /// <summary>Inicializa o writer com o dispatcher MediatR e o logger.</summary>
    public AuditServiceWriter(ISender sender, ILogger<AuditServiceWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(logger);

        _sender = sender;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Mapeia <see cref="AuditEntryRequest"/> → <see cref="RecordAuditEntryCommand"/> e envia via MediatR.
    /// Valida que <c>EntityId</c> é um UUID parseable antes do dispatch.
    /// </remarks>
    public async Task RecordAsync(AuditEntryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.EntityId, out var entityId))
        {
            _logger.LogWarning(
                "EntityId inválido recebido em AuditServiceWriter: EntityType={EntityType}",
                request.EntityType);

            throw new ArgumentException(
                $"EntityId '{request.EntityId}' não é um UUID válido.",
                nameof(request));
        }

        if (!Guid.TryParse(request.UserId, out var actorId))
        {
            _logger.LogWarning(
                "UserId inválido recebido em AuditServiceWriter: EntityType={EntityType}",
                request.EntityType);

            throw new ArgumentException(
                $"UserId '{request.UserId}' não é um UUID válido.",
                nameof(request));
        }

        var command = new RecordAuditEntryCommand
        {
            ActorId = actorId,
            EntityType = request.EntityType,
            EntityId = entityId,
            Action = MapAction(request.Action),
            RawBefore = request.RawBefore,
            RawAfter = request.RawAfter
        };

        await _sender.Send(command, cancellationToken);
    }

    // ------------------------------------------------------------------ Mapeamento de action

    private static DomainAction MapAction(AuditLog.Contracts.AuditAction contractAction) =>
        contractAction switch
        {
            AuditLog.Contracts.AuditAction.Create => DomainAction.Create,
            AuditLog.Contracts.AuditAction.Update => DomainAction.Update,
            AuditLog.Contracts.AuditAction.Delete => DomainAction.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(contractAction),
                $"AuditAction desconhecida no contrato: {contractAction}.")
        };
}
