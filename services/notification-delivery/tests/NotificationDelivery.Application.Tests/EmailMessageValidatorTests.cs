using FluentAssertions;
using NotificationDelivery.Application.Validation;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Application.Tests;

/// <summary>
/// Testes para <see cref="EmailMessageValidator"/>.
///
/// Garante que mensagens inválidas produzem <see cref="SendResult"/> de falha permanente
/// com os códigos corretos do catálogo, sem chamar qualquer provedor (Req 2.5, design §5.5).
/// </summary>
public sealed class EmailMessageValidatorTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static EmailMessage ValidMessage(
        string recipientEmail = "user@example.com",
        string subject = "Assunto válido",
        string htmlBody = "<p>Corpo válido</p>",
        string tenantId = "tenant-1",
        string correlationId = "corr-1") =>
        new(recipientEmail, subject, htmlBody, tenantId, correlationId);

    // -------------------------------------------------------------------------
    // NOTIF-ERR-001 — destinatário ausente ou malformado
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-07/ST-01a: destinatário malformado retorna PermanentFailure com NOTIF-ERR-001")]
    public void Validate_MalformedRecipient_ReturnsPermanentFailureWithErr001()
    {
        // Arrange — EmailMessage aceita qualquer string válida sintaticamente no construtor,
        // mas o validador de Application deve detectar outros problemas.
        // Para testar NOTIF-ERR-001 isoladamente, usamos o método estático de validação
        // que aceita a string bruta antes de construir EmailMessage.
        var validator = new EmailMessageValidator();

        // Act — validar uma string inválida diretamente
        var result = validator.ValidateRaw(
            recipientEmail: "nao-e-email",
            subject: "Assunto",
            htmlBody: "<p>corpo</p>",
            tenantId: "tenant-1",
            correlationId: "corr-1",
            provider: "test",
            attemptCount: 1);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(SendStatus.PermanentFailure);
        result.Reason.Should().NotBeNull();
        result.Reason!.Code.Should().Be(FailureCode.InvalidRecipient);
        result.Reason.IsRetriable.Should().BeFalse();
    }

    [Fact(DisplayName = "TASK-07/ST-01a: destinatário vazio retorna PermanentFailure com NOTIF-ERR-001")]
    public void Validate_EmptyRecipient_ReturnsPermanentFailureWithErr001()
    {
        var validator = new EmailMessageValidator();

        var result = validator.ValidateRaw(
            recipientEmail: "",
            subject: "Assunto",
            htmlBody: "<p>corpo</p>",
            tenantId: "tenant-1",
            correlationId: "corr-1",
            provider: "test",
            attemptCount: 1);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SendStatus.PermanentFailure);
        result.Reason!.Code.Should().Be(FailureCode.InvalidRecipient);
    }

    // -------------------------------------------------------------------------
    // NOTIF-ERR-002 — assunto ou corpo ausente
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-07/ST-01b: HtmlBody vazio retorna PermanentFailure com NOTIF-ERR-002")]
    public void Validate_EmptyHtmlBody_ReturnsPermanentFailureWithErr002()
    {
        var validator = new EmailMessageValidator();

        var result = validator.ValidateRaw(
            recipientEmail: "user@example.com",
            subject: "Assunto válido",
            htmlBody: "",
            tenantId: "tenant-1",
            correlationId: "corr-1",
            provider: "test",
            attemptCount: 1);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SendStatus.PermanentFailure);
        result.Reason!.Code.Should().Be(FailureCode.MissingSubjectOrBody);
        result.Reason.IsRetriable.Should().BeFalse();
    }

    [Fact(DisplayName = "TASK-07/ST-01b: Subject vazio retorna PermanentFailure com NOTIF-ERR-002")]
    public void Validate_EmptySubject_ReturnsPermanentFailureWithErr002()
    {
        var validator = new EmailMessageValidator();

        var result = validator.ValidateRaw(
            recipientEmail: "user@example.com",
            subject: "",
            htmlBody: "<p>corpo</p>",
            tenantId: "tenant-1",
            correlationId: "corr-1",
            provider: "test",
            attemptCount: 1);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SendStatus.PermanentFailure);
        result.Reason!.Code.Should().Be(FailureCode.MissingSubjectOrBody);
    }

    // -------------------------------------------------------------------------
    // Mensagem válida → retorna null (sem falha de validação)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-07/ST-01c: mensagem válida retorna null (sem falha de validação)")]
    public void Validate_ValidMessage_ReturnsNull()
    {
        var validator = new EmailMessageValidator();

        var result = validator.ValidateRaw(
            recipientEmail: "user@example.com",
            subject: "Assunto válido",
            htmlBody: "<p>Corpo válido</p>",
            tenantId: "tenant-1",
            correlationId: "corr-1",
            provider: "test",
            attemptCount: 1);

        result.Should().BeNull("mensagem válida não deve produzir falha de validação");
    }

    // -------------------------------------------------------------------------
    // Validate(EmailMessage) — sobrecarga tipada
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-07: Validate(EmailMessage) retorna null para mensagem válida")]
    public void Validate_EmailMessage_Valid_ReturnsNull()
    {
        var validator = new EmailMessageValidator();
        var message = ValidMessage();

        var result = validator.Validate(message, provider: "test", attemptCount: 1);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "TASK-07: RecipientEmail não aparece na mensagem de FailureReason (RNF 4)")]
    public void Validate_InvalidRecipient_ReasonMessageDoesNotContainEmail()
    {
        var validator = new EmailMessageValidator();
        const string email = "endereco-invalido-pii@";

        var result = validator.ValidateRaw(
            recipientEmail: email,
            subject: "Assunto",
            htmlBody: "<p>corpo</p>",
            tenantId: "tenant-1",
            correlationId: "corr-1",
            provider: "test",
            attemptCount: 1);

        result.Should().NotBeNull();
        result!.Reason!.Message.Should().NotContain(email,
            because: "mensagens de erro não devem expor PII (RNF 4)");
    }

    // -------------------------------------------------------------------------
    // CorrelationId e TenantId ausentes
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-07: CorrelationId vazio retorna PermanentFailure com NOTIF-ERR-002")]
    public void Validate_EmptyCorrelationId_ReturnsPermanentFailure()
    {
        var validator = new EmailMessageValidator();

        var result = validator.ValidateRaw(
            recipientEmail: "user@example.com",
            subject: "Assunto",
            htmlBody: "<p>corpo</p>",
            tenantId: "tenant-1",
            correlationId: "",
            provider: "test",
            attemptCount: 1);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SendStatus.PermanentFailure);
    }

    [Fact(DisplayName = "TASK-07: TenantId vazio retorna PermanentFailure com NOTIF-ERR-002")]
    public void Validate_EmptyTenantId_ReturnsPermanentFailure()
    {
        var validator = new EmailMessageValidator();

        var result = validator.ValidateRaw(
            recipientEmail: "user@example.com",
            subject: "Assunto",
            htmlBody: "<p>corpo</p>",
            tenantId: "",
            correlationId: "corr-1",
            provider: "test",
            attemptCount: 1);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SendStatus.PermanentFailure);
    }
}
