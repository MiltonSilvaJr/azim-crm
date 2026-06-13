using System.Reflection;
using FluentAssertions;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Contracts.Tests;

/// <summary>
/// Testes do value object <see cref="EmailMessage"/>.
/// Mapeia: Req 2, Req 2.1..2.5, RNF 4, design §4.3, §8.2, TASK-04/ST-01..ST-03.
///
/// Invariantes verificadas:
/// <list type="bullet">
///   <item><description>RecipientEmail inválido rejeitado com mensagem sem PII (RNF 4).</description></item>
///   <item><description>Subject, HtmlBody, TenantId, CorrelationId obrigatórios.</description></item>
///   <item><description>Imutabilidade — sem setter público (Req 2.3).</description></item>
///   <item><description>ToString() não expõe RecipientEmail (RNF 4).</description></item>
/// </list>
/// </summary>
public sealed class EmailMessageTests
{
    // -------------------------------------------------------------------------
    // Helpers de fixture
    // -------------------------------------------------------------------------

    private static EmailMessage ValidMessage() => new EmailMessage(
        recipientEmail: "usuario@exemplo.com.br",
        subject: "Digest diário",
        htmlBody: "<p>Conteúdo do digest</p>",
        tenantId: "tenant-001",
        correlationId: Guid.NewGuid().ToString());

    // -------------------------------------------------------------------------
    // Construção válida
    // -------------------------------------------------------------------------

    /// <summary>
    /// EmailMessage com campos obrigatórios válidos deve construir sem erro.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com campos válidos deve construir sem erro")]
    public void EmailMessage_ValidFields_ShouldConstruct()
    {
        // Arrange + Act
        var act = () => ValidMessage();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// EmailMessage deve expor os campos fornecidos com os valores corretos.
    /// </summary>
    [Fact(DisplayName = "EmailMessage deve expor os campos obrigatórios com os valores fornecidos")]
    public void EmailMessage_ValidFields_ShouldExposeFields()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var msg = new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: "Assunto teste",
            htmlBody: "<p>Corpo HTML</p>",
            tenantId: "tenant-123",
            correlationId: correlationId);

        // Assert
        msg.Subject.Should().Be("Assunto teste");
        msg.HtmlBody.Should().Be("<p>Corpo HTML</p>");
        msg.TenantId.Should().Be("tenant-123");
        msg.CorrelationId.Should().Be(correlationId);
    }

    // -------------------------------------------------------------------------
    // Imutabilidade (TASK-04/ST-01(f), Req 2.3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// EmailMessage deve ser imutável após construção — sem setter público de instância.
    /// Mapeia: TASK-04/ST-01(f), Req 2.3, design §4.3.
    /// </summary>
    [Fact(DisplayName = "EmailMessage deve ser imutável após construção (Req 2.3)")]
    public void EmailMessage_ShouldBeImmutable_AfterConstruction()
    {
        // Act — verificar via reflexão que não há setters públicos de instância
        var publicSetters = typeof(EmailMessage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        // Assert
        publicSetters.Should().BeEmpty(
            because: "EmailMessage é um value object imutável — nenhuma propriedade deve ter setter público (Req 2.3)");
    }

    // -------------------------------------------------------------------------
    // Validação de RecipientEmail (TASK-04/ST-01(a), Req 2.1, RNF 4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// RecipientEmail inválido deve lançar ArgumentException sem o endereço na mensagem.
    /// Mapeia: TASK-04/ST-01(a), Req 2.1, RNF 4.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com RecipientEmail inválido deve lançar ArgumentException sem o endereço na mensagem (RNF 4)")]
    public void EmailMessage_WithInvalidRecipient_ShouldThrow_WithoutPiiInMessage()
    {
        // Arrange
        const string invalidEmail = "nao-e-email";

        // Act
        var act = () => new EmailMessage(
            recipientEmail: invalidEmail,
            subject: "Assunto",
            htmlBody: "<p>Corpo</p>",
            tenantId: "tenant-001",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .Which.Message.Should().NotContain(invalidEmail,
                because: "a mensagem de erro não deve expor o e-mail do destinatário em claro (RNF 4)");
    }

    /// <summary>
    /// RecipientEmail com apenas nome de usuário (sem domínio) deve ser rejeitado.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com RecipientEmail sem @ deve lançar ArgumentException")]
    public void EmailMessage_WithEmailWithoutAtSign_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new EmailMessage(
            recipientEmail: "semAt",
            subject: "Assunto",
            htmlBody: "<p>Corpo</p>",
            tenantId: "tenant-001",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("recipientEmail");
    }

    /// <summary>
    /// RecipientEmail nulo deve lançar ArgumentException sem PII.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com RecipientEmail nulo deve lançar ArgumentException")]
    public void EmailMessage_WithNullRecipient_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new EmailMessage(
            recipientEmail: null!,
            subject: "Assunto",
            htmlBody: "<p>Corpo</p>",
            tenantId: "tenant-001",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("recipientEmail");
    }

    // -------------------------------------------------------------------------
    // Validação de Subject (TASK-04/ST-01(b), Req 2.1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Subject vazio deve lançar ArgumentException.
    /// Mapeia: TASK-04/ST-01(b), Req 2.1.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com Subject vazio deve lançar ArgumentException (Req 2.1)")]
    public void EmailMessage_WithEmptySubject_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: string.Empty,
            htmlBody: "<p>Corpo</p>",
            tenantId: "tenant-001",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("subject");
    }

    // -------------------------------------------------------------------------
    // Validação de HtmlBody (TASK-04/ST-01(c), Req 2.1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// HtmlBody vazio deve lançar ArgumentException.
    /// Mapeia: TASK-04/ST-01(c), Req 2.1.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com HtmlBody vazio deve lançar ArgumentException (Req 2.1)")]
    public void EmailMessage_WithEmptyHtmlBody_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: "Assunto",
            htmlBody: string.Empty,
            tenantId: "tenant-001",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("htmlBody");
    }

    // -------------------------------------------------------------------------
    // Validação de TenantId (TASK-04/ST-01(d), Req 2.2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// TenantId nulo deve lançar ArgumentException.
    /// Mapeia: TASK-04/ST-01(d), Req 2.2.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com TenantId nulo deve lançar ArgumentException (Req 2.2)")]
    public void EmailMessage_WithNullTenantId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: "Assunto",
            htmlBody: "<p>Corpo</p>",
            tenantId: null!,
            correlationId: Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("tenantId");
    }

    /// <summary>
    /// TenantId vazio deve lançar ArgumentException.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com TenantId vazio deve lançar ArgumentException")]
    public void EmailMessage_WithEmptyTenantId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: "Assunto",
            htmlBody: "<p>Corpo</p>",
            tenantId: string.Empty,
            correlationId: Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("tenantId");
    }

    // -------------------------------------------------------------------------
    // Validação de CorrelationId (TASK-04/ST-01(e), Req 2.2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// CorrelationId nulo deve lançar ArgumentException.
    /// Mapeia: TASK-04/ST-01(e), Req 2.2.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com CorrelationId nulo deve lançar ArgumentException (Req 2.2)")]
    public void EmailMessage_WithNullCorrelationId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: "Assunto",
            htmlBody: "<p>Corpo</p>",
            tenantId: "tenant-001",
            correlationId: null!);

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("correlationId");
    }

    /// <summary>
    /// CorrelationId vazio deve lançar ArgumentException.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com CorrelationId vazio deve lançar ArgumentException")]
    public void EmailMessage_WithEmptyCorrelationId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: "Assunto",
            htmlBody: "<p>Corpo</p>",
            tenantId: "tenant-001",
            correlationId: string.Empty);

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("correlationId");
    }

    // -------------------------------------------------------------------------
    // Anti-PII em ToString() (TASK-04/ST-03, RNF 4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// ToString() não deve expor o RecipientEmail em claro.
    /// Mapeia: TASK-04/ST-03, RNF 4, design §8.2.
    /// </summary>
    [Fact(DisplayName = "EmailMessage.ToString() não deve expor RecipientEmail (RNF 4)")]
    public void EmailMessage_ToString_ShouldNotExposeRecipientEmail()
    {
        // Arrange
        const string email = "usuario.secreto@exemplo.com.br";
        var msg = new EmailMessage(
            recipientEmail: email,
            subject: "Assunto",
            htmlBody: "<p>Corpo</p>",
            tenantId: "tenant-001",
            correlationId: Guid.NewGuid().ToString());

        // Act
        var str = msg.ToString();

        // Assert
        str.Should().NotContain(email,
            because: "RecipientEmail é PII e não deve aparecer em ToString() (RNF 4)");
        str.Should().NotContain("usuario.secreto",
            because: "o nome de usuário do e-mail é PII e não deve aparecer em ToString() (RNF 4)");
    }

    // -------------------------------------------------------------------------
    // Campos opcionais
    // -------------------------------------------------------------------------

    /// <summary>
    /// PlainTextBody é opcional e deve ser null quando não fornecido.
    /// Mapeia: design §8.2 (Req 6.3 — derivado pelo renderer quando ausente).
    /// </summary>
    [Fact(DisplayName = "EmailMessage sem PlainTextBody deve ter PlainTextBody nulo")]
    public void EmailMessage_WithoutPlainTextBody_ShouldHaveNullPlainTextBody()
    {
        // Arrange + Act
        var msg = ValidMessage();

        // Assert
        msg.PlainTextBody.Should().BeNull();
    }

    /// <summary>
    /// Branding é opcional e deve ser null quando não fornecido (tema padrão pelo decorator).
    /// Mapeia: design §8.2, Req 5.3.
    /// </summary>
    [Fact(DisplayName = "EmailMessage sem Branding deve ter Branding nulo (tema padrão pelo decorator)")]
    public void EmailMessage_WithoutBranding_ShouldHaveNullBranding()
    {
        // Arrange + Act
        var msg = ValidMessage();

        // Assert
        msg.Branding.Should().BeNull();
    }

    /// <summary>
    /// IdempotencyKey é opcional e deve ser null quando não fornecido.
    /// Mapeia: design §8.2, Req 9, DD-005.
    /// </summary>
    [Fact(DisplayName = "EmailMessage sem IdempotencyKey deve ter IdempotencyKey nulo")]
    public void EmailMessage_WithoutIdempotencyKey_ShouldHaveNullIdempotencyKey()
    {
        // Arrange + Act
        var msg = ValidMessage();

        // Assert
        msg.IdempotencyKey.Should().BeNull();
    }

    /// <summary>
    /// EmailMessage com todos os campos opcionais fornecidos deve construir sem erro.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com todos os campos opcionais deve construir sem erro")]
    public void EmailMessage_WithAllOptionalFields_ShouldConstruct()
    {
        // Arrange
        var branding = new BrandingConfig("https://cdn.azim.com.br/logo.png", "#0F4C81", "#FFFFFF");
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var act = () => new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: "Assunto completo",
            htmlBody: "<p>HTML</p>",
            tenantId: "tenant-001",
            correlationId: correlationId,
            plainTextBody: "Versão texto puro",
            branding: branding,
            idempotencyKey: "idem-key-001");

        // Assert
        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // Limites de tamanho (TASK-04/ST-03)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Subject que excede o limite máximo deve lançar ArgumentException.
    /// Mapeia: TASK-04/ST-03 — extrair limites como constantes.
    /// </summary>
    [Fact(DisplayName = "EmailMessage com Subject acima do limite deve lançar ArgumentException")]
    public void EmailMessage_WithSubjectExceedingLimit_ShouldThrowArgumentException()
    {
        // Arrange — Subject > 998 caracteres (RFC 5321 / RFC 2822 limita linha a 998 chars)
        var longSubject = new string('A', EmailMessage.MaxSubjectLength + 1);

        // Act
        var act = () => new EmailMessage(
            recipientEmail: "usuario@exemplo.com.br",
            subject: longSubject,
            htmlBody: "<p>Corpo</p>",
            tenantId: "tenant-001",
            correlationId: Guid.NewGuid().ToString());

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("subject");
    }
}
