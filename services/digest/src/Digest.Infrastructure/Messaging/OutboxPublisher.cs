using System.Text.Json;
using Digest.Application.Abstractions;
using Digest.Domain.Events;
using Digest.Infrastructure.Persistence;

namespace Digest.Infrastructure.Messaging;

/// <summary>
/// Implementação de <see cref="IOutboxPublisher"/> que grava <see cref="DigestEmailSent"/>
/// em <c>outbox_messages</c> dentro da transação corrente do DbContext (DD-009, ADR-0004).
/// </summary>
/// <remarks>
/// O <see cref="DigestDbContext"/> deve já estar dentro de uma transação aberta
/// (gerenciada pelo <c>UnitOfWorkBehavior</c> ou pelo caller). Se não houver transação,
/// o EF Core cria uma implícita para o <c>SaveChangesAsync</c>.
/// O payload serializado não contém PII: nenhum campo de e-mail ou conteúdo (RNF 10.2).
/// </remarks>
public sealed class OutboxPublisher : IOutboxPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    private readonly DigestDbContext _db;

    /// <summary>
    /// Constrói o publisher com o DbContext injetado (Scoped — mesma instância do caller).
    /// </summary>
    public OutboxPublisher(DigestDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Serializa o evento como JSON e insere em <c>outbox_messages</c> via EF Core.
    /// O <c>SaveChangesAsync</c> persiste o registro na transação corrente —
    /// se houver rollback externo, o INSERT também é desfeito (DD-009).
    /// O token em claro nunca transita pelo publisher (DD-011).
    /// </remarks>
    public async Task PublishAsync(DigestEmailSent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Serializa payload sem PII (RNF 10.2): tenant_id, user_id, digest_date, message_id, occurred_at
        // DigestDate.Value (NodaTime.LocalDate) precisa de serialização customizada
        var payload = JsonSerializer.Serialize(new
        {
            tenant_id = domainEvent.TenantId,
            user_id = domainEvent.UserId,
            digest_date = domainEvent.DigestDate.Value.ToString("yyyy-MM-dd", null),
            message_id = domainEvent.MessageId,
            occurred_at = domainEvent.OccurredAt,
        }, SerializerOptions);

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(DigestEmailSent),
            Payload = payload,
            OccurredAt = domainEvent.OccurredAt,
            ProcessedAt = null,
            TenantId = domainEvent.TenantId,
        };

        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
