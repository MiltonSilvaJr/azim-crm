using Digest.Domain.Entities;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using NodaTime;

namespace Digest.Domain.Tests.Entities;

/// <summary>
/// Testes unitários para <see cref="EmailDigestLog"/> e sua state machine (TASK-05).
/// Valida: transições válidas, transições inválidas, terminais, digest_date local.
/// </summary>
public sealed class EmailDigestLogTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DigestDate Date = new(new LocalDate(2026, 6, 15));

    // ---------------------------------------------------------------
    // Criação
    // ---------------------------------------------------------------

    [Fact]
    public void Create_produces_entity_in_scheduled_status()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.Status.Should().Be(DigestStatus.Scheduled);
        log.TenantId.Should().Be(TenantId);
        log.UserId.Should().Be(UserId);
        log.DigestDate.Should().Be(Date);
    }

    [Fact]
    public void Create_requires_non_empty_tenant_id()
    {
        var act = () => EmailDigestLog.Schedule(Guid.Empty, UserId, Date, correlationId: null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_requires_non_empty_user_id()
    {
        var act = () => EmailDigestLog.Schedule(TenantId, Guid.Empty, Date, correlationId: null);
        act.Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------------------
    // Transições válidas
    // ---------------------------------------------------------------

    [Fact]
    public void MarkSent_transitions_from_scheduled_to_sent()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkSent("msg-id-123");
        log.Status.Should().Be(DigestStatus.Sent);
        log.MessageId.Should().Be("msg-id-123");
        log.SentAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_transitions_from_scheduled_to_failed()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkFailed();
        log.Status.Should().Be(DigestStatus.Failed);
        log.FailedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkDelivered_transitions_from_sent_to_delivered()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkSent("msg-id");
        log.MarkDelivered();
        log.Status.Should().Be(DigestStatus.Delivered);
        log.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkOpened_transitions_from_delivered_to_opened()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkSent("msg-id");
        log.MarkDelivered();
        log.MarkOpened();
        log.Status.Should().Be(DigestStatus.Opened);
        log.OpenedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkBounced_transitions_from_sent_to_bounced()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkSent("msg-id");
        log.MarkBounced();
        log.Status.Should().Be(DigestStatus.Bounced);
    }

    // ---------------------------------------------------------------
    // Transições inválidas
    // ---------------------------------------------------------------

    [Fact]
    public void MarkSent_from_non_scheduled_throws()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkSent("msg-id");
        var act = () => log.MarkSent("msg-id-2");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkDelivered_from_scheduled_throws()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        var act = () => log.MarkDelivered();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkOpened_from_sent_throws()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkSent("msg-id");
        var act = () => log.MarkOpened();
        act.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------
    // Terminais
    // ---------------------------------------------------------------

    [Fact]
    public void Failed_is_terminal_rejects_MarkSent()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkFailed();
        var act = () => log.MarkSent("msg");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Bounced_is_terminal_rejects_MarkDelivered()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkSent("msg");
        log.MarkBounced();
        var act = () => log.MarkDelivered();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Opened_is_terminal_rejects_MarkBounced()
    {
        var log = EmailDigestLog.Schedule(TenantId, UserId, Date, correlationId: null);
        log.MarkSent("msg");
        log.MarkDelivered();
        log.MarkOpened();
        var act = () => log.MarkBounced();
        act.Should().Throw<InvalidOperationException>();
    }

    // ---------------------------------------------------------------
    // digest_date é data local (não UTC)
    // ---------------------------------------------------------------

    [Fact]
    public void DigestDate_holds_local_date_not_utc()
    {
        var localDate = new DigestDate(new LocalDate(2026, 6, 15));
        var log = EmailDigestLog.Schedule(TenantId, UserId, localDate, correlationId: null);
        log.DigestDate.Value.Should().Be(new LocalDate(2026, 6, 15));
    }
}
