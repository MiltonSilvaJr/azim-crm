using Digest.Application.Repositories;
using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Digest.Infrastructure.Repositories;

/// <summary>
/// Implementação de <see cref="IEmailDigestLogRepository"/> usando EF Core 9 e PostgreSQL.
/// Reserva de idempotência via <c>INSERT ... ON CONFLICT DO NOTHING</c> (DD-008).
/// Protegida por Global Query Filter (ADR-0001) e RLS falha-fechada (TASK-15).
/// </summary>
public sealed class EmailDigestLogRepository : IEmailDigestLogRepository
{
    private readonly DigestDbContext _db;

    /// <summary>
    /// Constrói o repositório com o DbContext injetado.
    /// </summary>
    public EmailDigestLogRepository(DigestDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Implementação via Raw SQL para garantir <c>ON CONFLICT DO NOTHING</c> atômico (DD-008).
    /// O EF Core não gera ON CONFLICT nativamente; a query manual é a única forma segura.
    /// Retorna o número de linhas afetadas: 1 = reservado, 0 = conflito (já existe).
    /// </remarks>
    public async Task<ReservationResult> ReserveAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        Guid? correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(digestDate);
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));
        if (userId == Guid.Empty)
            throw new ArgumentException("user_id não pode ser vazio.", nameof(userId));

        var id = Guid.NewGuid();
        var scheduledAt = DateTimeOffset.UtcNow;
        var dateValue = digestDate.Value; // NodaTime.LocalDate

        // INSERT ON CONFLICT DO NOTHING — operação atômica de reserva (DD-008, RNF 2.3)
        // A UNIQUE (tenant_id, user_id, digest_date) garante que somente um worker vence a corrida.
        // Rows afetadas = 1 → Reserved; 0 → conflito (outro worker ou estado terminal).
        var rowsAffected = await _db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO email_digest_logs
                 (id, tenant_id, user_id, digest_date, status, correlation_id, scheduled_at)
             VALUES
                 ({id}, {tenantId}, {userId}, {dateValue}, 'scheduled', {correlationId}, {scheduledAt})
             ON CONFLICT (tenant_id, user_id, digest_date) DO NOTHING
             """,
            cancellationToken);

        if (rowsAffected == 1)
            return ReservationResult.Reserved;

        // Conflito detectado — lê o registro existente para determinar o resultado correto
        // (o Global Query Filter garante que somente o tenant correto é consultado)
        var existing = await _db.EmailDigestLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.TenantId == tenantId && l.UserId == userId && l.DigestDate == digestDate,
                cancellationToken);

        if (existing is null)
            return ReservationResult.AlreadyScheduled; // RLS ou condição de corrida — trata como scheduled

        return existing.Status is DigestStatus.Sent or DigestStatus.Delivered or DigestStatus.Opened
            ? ReservationResult.AlreadySent
            : ReservationResult.AlreadyScheduled;
    }

    /// <inheritdoc/>
    public async Task MarkSentAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(digestDate);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var sentAt = DateTimeOffset.UtcNow;

        await _db.Database.ExecuteSqlAsync(
            $"""
             UPDATE email_digest_logs
             SET status = 'sent',
                 message_id = {messageId},
                 sent_at = {sentAt}
             WHERE tenant_id = {tenantId}
               AND user_id = {userId}
               AND digest_date = {digestDate.Value}
               AND status = 'scheduled'
             """,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task MarkFailedAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(digestDate);

        var failedAt = DateTimeOffset.UtcNow;

        await _db.Database.ExecuteSqlAsync(
            $"""
             UPDATE email_digest_logs
             SET status = 'failed',
                 failed_at = {failedAt}
             WHERE tenant_id = {tenantId}
               AND user_id = {userId}
               AND digest_date = {digestDate.Value}
               AND status = 'scheduled'
             """,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateStatusByMessageIdAsync(
        string messageId,
        DigestStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        var now = DateTimeOffset.UtcNow;

        // Atualização cross-tenant por messageId — idempotente (UPDATE aplica somente na transição válida).
        // Cada switch arm usa ExecuteSqlAsync com string interpolada segura (FormattableString com parâmetros).
        switch (newStatus)
        {
            case DigestStatus.Delivered:
                await _db.Database.ExecuteSqlAsync(
                    $"UPDATE email_digest_logs SET status = 'delivered', delivered_at = {now} WHERE message_id = {messageId} AND status = 'sent'",
                    cancellationToken);
                break;

            case DigestStatus.Opened:
                await _db.Database.ExecuteSqlAsync(
                    $"UPDATE email_digest_logs SET status = 'opened', opened_at = {now} WHERE message_id = {messageId} AND status = 'delivered'",
                    cancellationToken);
                break;

            case DigestStatus.Bounced:
                await _db.Database.ExecuteSqlAsync(
                    $"UPDATE email_digest_logs SET status = 'bounced' WHERE message_id = {messageId} AND status = 'sent'",
                    cancellationToken);
                break;

            default:
                throw new ArgumentException($"Status inválido para UpdateStatusByMessageId: {newStatus}", nameof(newStatus));
        }
    }

    // Método auxiliar para leitura direta por chave (utilizado em testes e handlers futuros)
    /// <summary>
    /// Localiza um <see cref="EmailDigestLog"/> pelo triplo chave natural (Global Query Filter ativo).
    /// </summary>
    public Task<EmailDigestLog?> GetByKeyAsync(
        Guid tenantId,
        Guid userId,
        DigestDate digestDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(digestDate);

        return _db.EmailDigestLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.TenantId == tenantId && l.UserId == userId && l.DigestDate == digestDate,
                cancellationToken);
    }
}
