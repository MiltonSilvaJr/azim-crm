using Digest.Domain.Aggregates;
using Digest.Domain.Enums;
using Digest.Domain.Events;
using Digest.Domain.ValueObjects;
using NodaTime;

namespace Digest.Domain.Tests.Aggregates;

/// <summary>
/// Testes de invariante para <see cref="DigestJob"/> (aggregate root) e <see cref="DigestEmailSent"/> (TASK-08).
/// Valida: tenant_id obrigatório, azimute apenas segunda + gestão, exatamente um evento por envio,
/// ausência de PII no evento.
/// </summary>
public sealed class DigestJobTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // Data de segunda-feira local
    private static readonly DigestDate MondayDate = new(new LocalDate(2026, 6, 15));

    // Data de terça-feira local
    private static readonly DigestDate TuesdayDate = new(new LocalDate(2026, 6, 16));

    // ---------------------------------------------------------------
    // Criação / invariantes
    // ---------------------------------------------------------------

    [Fact]
    public void Create_requires_non_empty_tenant_id()
    {
        var act = () => DigestJob.Create(Guid.Empty, MondayDate);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_stores_tenant_id_and_digest_date()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        job.TenantId.Should().Be(TenantId);
        job.DigestDate.Should().Be(MondayDate);
    }

    [Fact]
    public void New_job_has_no_domain_events()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        job.DomainEvents.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Azimute: só segunda + gestão
    // ---------------------------------------------------------------

    [Fact]
    public void ShouldIncludeAzimute_returns_true_on_monday_for_gestorbu()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        job.ShouldIncludeAzimute(RecipientPapel.GestorBU).Should().BeTrue();
    }

    [Fact]
    public void ShouldIncludeAzimute_returns_true_on_monday_for_tadmin()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        job.ShouldIncludeAzimute(RecipientPapel.TAdmin).Should().BeTrue();
    }

    [Fact]
    public void ShouldIncludeAzimute_returns_false_on_tuesday_for_gestorbu()
    {
        var job = DigestJob.Create(TenantId, TuesdayDate);
        job.ShouldIncludeAzimute(RecipientPapel.GestorBU).Should().BeFalse();
    }

    [Fact]
    public void ShouldIncludeAzimute_returns_false_on_monday_for_vendedor()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        job.ShouldIncludeAzimute(RecipientPapel.Vendedor).Should().BeFalse();
    }

    [Fact]
    public void ShouldIncludeAzimute_returns_false_on_monday_for_viewer()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        job.ShouldIncludeAzimute(RecipientPapel.Viewer).Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // RegisterSent: exatamente um DigestEmailSent por usuário
    // ---------------------------------------------------------------

    [Fact]
    public void RegisterSent_adds_exactly_one_DigestEmailSent_event()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        var userId = Guid.NewGuid();

        job.RegisterSent(userId, messageId: "msg-001");

        job.DomainEvents.Should().HaveCount(1);
        job.DomainEvents[0].Should().BeOfType<DigestEmailSent>();
    }

    [Fact]
    public void RegisterSent_twice_for_same_user_generates_two_events()
    {
        // O aggregate não impede chamada dupla — a idempotência é garantida pelo EmailDigestLog.
        // Mas cada RegisterSent bem-sucedido deve gerar exatamente um evento.
        var job = DigestJob.Create(TenantId, MondayDate);
        var userId = Guid.NewGuid();

        job.RegisterSent(userId, "msg-001");
        job.RegisterSent(userId, "msg-002");

        // Cada chamada gera seu próprio evento (destinado ao Outbox; deduplicação é por EmailDigestLog)
        job.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void RegisterSent_event_carries_tenant_user_date_messageId_and_occurred_at()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        var userId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        job.RegisterSent(userId, "msg-123");

        var evt = job.DomainEvents.OfType<DigestEmailSent>().Single();
        evt.TenantId.Should().Be(TenantId);
        evt.UserId.Should().Be(userId);
        evt.DigestDate.Should().Be(MondayDate);
        evt.MessageId.Should().Be("msg-123");
        evt.OccurredAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void RegisterSent_event_does_not_contain_pii_fields()
    {
        // Verifica estruturalmente que o evento não tem propriedades de PII (RNF 3.4, RNF 10.2)
        var properties = typeof(DigestEmailSent).GetProperties();
        var hasPii = properties.Any(p =>
            p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Name", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Content", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Subject", StringComparison.OrdinalIgnoreCase));

        hasPii.Should().BeFalse("DigestEmailSent não deve conter PII (RNF 3.4, RNF 10.2)");
    }

    [Fact]
    public void ClearEvents_empties_domain_events()
    {
        var job = DigestJob.Create(TenantId, MondayDate);
        job.RegisterSent(Guid.NewGuid(), "msg-001");
        job.ClearEvents();
        job.DomainEvents.Should().BeEmpty();
    }
}
