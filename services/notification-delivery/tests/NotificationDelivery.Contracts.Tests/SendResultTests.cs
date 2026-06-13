using System.Reflection;
using FluentAssertions;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Contracts.Tests;

/// <summary>
/// Testes do value object <see cref="SendResult"/> e de <see cref="IEmailSender"/>.
/// Mapeia: Req 1, Req 3, Req 3.1..3.5, design §4.3, §8.3, TASK-06/ST-01..ST-03.
///
/// Invariantes verificadas:
/// <list type="bullet">
///   <item><description><see cref="SendResult"/> com <see cref="SendStatus.Sent"/> sem MessageId lança na construção.</description></item>
///   <item><description><see cref="SendResult"/> com status de falha sem Reason lança na construção.</description></item>
///   <item><description>Imutabilidade — sem setter público.</description></item>
///   <item><description>CorrelationId sempre propagado.</description></item>
///   <item><description>IEmailSender sem tipo de provedor na assinatura.</description></item>
/// </list>
/// </summary>
public sealed class SendResultTests
{
    // -------------------------------------------------------------------------
    // Helpers de fixture
    // -------------------------------------------------------------------------

    private static readonly string SampleCorrelationId = Guid.NewGuid().ToString();

    private static FailureReason TransientReason() =>
        new FailureReason(FailureCode.TransientProviderFailure, "Falha transiente", IsRetriable: true);

    private static FailureReason PermanentReason() =>
        new FailureReason(FailureCode.InvalidRecipient, "Destinatário inválido", IsRetriable: false);

    // -------------------------------------------------------------------------
    // Construção válida — Sent
    // -------------------------------------------------------------------------

    /// <summary>
    /// SendResult Sent com MessageId válido deve construir sem erro.
    /// Mapeia: design §8.3, TASK-06/ST-02.
    /// </summary>
    [Fact(DisplayName = "SendResult Sent com MessageId deve construir sem erro")]
    public void SendResult_Sent_WithMessageId_ShouldConstruct()
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: SendStatus.Sent,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: "msg-123",
            reason: null);

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// SendResult Sent deve ter MessageId não nulo.
    /// Mapeia: design §8.3 ("MessageId presente sse Sent").
    /// </summary>
    [Fact(DisplayName = "SendResult Sent deve ter MessageId presente")]
    public void SendResult_Sent_ShouldHaveMessageId()
    {
        // Arrange + Act
        var result = new SendResult(
            status: SendStatus.Sent,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: "msg-abc",
            reason: null);

        // Assert
        result.MessageId.Should().Be("msg-abc");
        result.Reason.Should().BeNull(because: "SendResult Sent não deve ter Reason (design §8.3)");
    }

    // -------------------------------------------------------------------------
    // Invariante: Sent sem MessageId lança (TASK-06/ST-01(a))
    // -------------------------------------------------------------------------

    /// <summary>
    /// SendResult com Status=Sent sem MessageId deve lançar na construção.
    /// Mapeia: TASK-06/ST-01(a), design §4.3, §8.3.
    /// </summary>
    [Fact(DisplayName = "SendResult Sent sem MessageId deve lançar ArgumentException (TASK-06)")]
    public void SendResult_Sent_WithoutMessageId_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: SendStatus.Sent,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: null,
            reason: null);

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // Invariante: status de falha sem Reason lança (TASK-06/ST-01(b))
    // -------------------------------------------------------------------------

    /// <summary>
    /// SendResult TransientFailure sem Reason deve lançar na construção.
    /// Mapeia: TASK-06/ST-01(b), design §4.3, §8.3.
    /// </summary>
    [Fact(DisplayName = "SendResult TransientFailure sem Reason deve lançar ArgumentException (TASK-06)")]
    public void SendResult_TransientFailure_WithoutReason_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: SendStatus.TransientFailure,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 3,
            messageId: null,
            reason: null);

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
    }

    /// <summary>
    /// SendResult PermanentFailure sem Reason deve lançar na construção.
    /// Mapeia: TASK-06/ST-01(b), design §4.3, §8.3.
    /// </summary>
    [Fact(DisplayName = "SendResult PermanentFailure sem Reason deve lançar ArgumentException (TASK-06)")]
    public void SendResult_PermanentFailure_WithoutReason_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: SendStatus.PermanentFailure,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: null,
            reason: null);

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
    }

    /// <summary>
    /// SendResult Bounced sem Reason deve lançar na construção.
    /// </summary>
    [Fact(DisplayName = "SendResult Bounced sem Reason deve lançar ArgumentException")]
    public void SendResult_Bounced_WithoutReason_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: SendStatus.Bounced,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: null,
            reason: null);

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
    }

    /// <summary>
    /// SendResult Suppressed sem Reason deve lançar na construção.
    /// </summary>
    [Fact(DisplayName = "SendResult Suppressed sem Reason deve lançar ArgumentException")]
    public void SendResult_Suppressed_WithoutReason_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: SendStatus.Suppressed,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: null,
            reason: null);

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // Invariante: falha com Reason válido deve construir (ST-02)
    // -------------------------------------------------------------------------

    /// <summary>
    /// SendResult de falha com Reason válido deve construir sem erro.
    /// </summary>
    [Theory(DisplayName = "SendResult de falha com Reason deve construir sem erro")]
    [InlineData(SendStatus.TransientFailure)]
    [InlineData(SendStatus.PermanentFailure)]
    [InlineData(SendStatus.Bounced)]
    [InlineData(SendStatus.Suppressed)]
    public void SendResult_Failure_WithReason_ShouldConstruct(SendStatus status)
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: status,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: null,
            reason: TransientReason());

        // Assert
        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // Invariante: MessageId nulo para status diferente de Sent (design §8.3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// SendResult de falha deve ter MessageId nulo.
    /// Mapeia: design §8.3 ("MessageId presente sse Sent").
    /// </summary>
    [Fact(DisplayName = "SendResult TransientFailure deve ter MessageId nulo (design §8.3)")]
    public void SendResult_TransientFailure_ShouldHaveNullMessageId()
    {
        // Arrange + Act
        var result = new SendResult(
            status: SendStatus.TransientFailure,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 2,
            messageId: null,
            reason: TransientReason());

        // Assert
        result.MessageId.Should().BeNull(because: "MessageId só deve estar presente quando Status=Sent (design §8.3)");
    }

    // -------------------------------------------------------------------------
    // Invariante: CorrelationId sempre propagado (TASK-06/ST-01(d))
    // -------------------------------------------------------------------------

    /// <summary>
    /// CorrelationId de SendResult deve ser igual ao da mensagem de origem.
    /// Mapeia: TASK-06/ST-01(d), design §8.3, Req 3.2.
    /// </summary>
    [Fact(DisplayName = "SendResult deve propagar CorrelationId igual ao da mensagem de origem (Req 3.2)")]
    public void SendResult_ShouldPropagateCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var result = new SendResult(
            status: SendStatus.Sent,
            correlationId: correlationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: "msg-xyz",
            reason: null);

        // Assert
        result.CorrelationId.Should().Be(correlationId,
            because: "CorrelationId deve ser propagado em todos os casos (design §8.3, Req 3.2)");
    }

    /// <summary>
    /// CorrelationId ausente (nulo/vazio) deve lançar ArgumentException.
    /// </summary>
    [Fact(DisplayName = "SendResult sem CorrelationId deve lançar ArgumentException")]
    public void SendResult_WithoutCorrelationId_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: SendStatus.Sent,
            correlationId: null!,
            provider: "postmark",
            attemptCount: 1,
            messageId: "msg-001",
            reason: null);

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("correlationId");
    }

    // -------------------------------------------------------------------------
    // Imutabilidade (TASK-06/ST-01(c))
    // -------------------------------------------------------------------------

    /// <summary>
    /// SendResult deve ser imutável após construção — sem setter público de instância.
    /// Mapeia: TASK-06/ST-01(c), design §4.3.
    /// </summary>
    [Fact(DisplayName = "SendResult deve ser imutável após construção (design §4.3)")]
    public void SendResult_ShouldBeImmutable_AfterConstruction()
    {
        // Act — verificar via reflexão que não há setters públicos de instância
        var publicSetters = typeof(SendResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        // Assert
        publicSetters.Should().BeEmpty(
            because: "SendResult é um value object imutável — nenhuma propriedade deve ter setter público (design §4.3)");
    }

    // -------------------------------------------------------------------------
    // Sent com Reason deve lançar (Reason deve ser null para Sent)
    // -------------------------------------------------------------------------

    /// <summary>
    /// SendResult Sent com Reason não nulo deve lançar — Reason deve ser nulo quando Sent.
    /// Mapeia: design §8.3 ("Reason presente sse falha").
    /// </summary>
    [Fact(DisplayName = "SendResult Sent com Reason não nulo deve lançar ArgumentException")]
    public void SendResult_Sent_WithReason_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new SendResult(
            status: SendStatus.Sent,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: "msg-ok",
            reason: PermanentReason()); // Reason não deve existir em Sent

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // IEmailSender — verificação de tipo por reflexão (TASK-06/ST-02, Req 1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// IEmailSender deve ser uma interface pública em NotificationDelivery.Contracts.
    /// Mapeia: TASK-06/ST-02, Req 1, design §8.1.
    /// </summary>
    [Fact(DisplayName = "IEmailSender deve ser interface pública em NotificationDelivery.Contracts (Req 1)")]
    public void IEmailSender_ShouldBePublicInterface_InContractsAssembly()
    {
        // Arrange
        var type = typeof(IEmailSender);

        // Assert
        type.IsInterface.Should().BeTrue(because: "IEmailSender é a interface pública do contrato (Req 1)");
        type.IsPublic.Should().BeTrue(because: "a interface deve ser pública para ser consumida por injeção de dependência");
        type.Assembly.GetName().Name.Should().Be("NotificationDelivery.Contracts",
            because: "IEmailSender deve residir em Contracts, nunca em Application ou Infrastructure (design §3, Req 1.2)");
    }

    /// <summary>
    /// IEmailSender não deve ter parâmetros de tipo de provedor na assinatura de SendAsync.
    /// Mapeia: TASK-06/ST-02, Req 1.2, design §8.1.
    /// </summary>
    [Fact(DisplayName = "IEmailSender.SendAsync deve ter assinatura sem tipo de provedor (Req 1.2)")]
    public void IEmailSender_SendAsync_ShouldHaveCorrectSignature()
    {
        // Arrange
        var method = typeof(IEmailSender).GetMethod("SendAsync");

        // Assert
        method.Should().NotBeNull(because: "IEmailSender deve ter método SendAsync");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(2,
            because: "SendAsync(EmailMessage, CancellationToken) tem 2 parâmetros (design §8.1)");
        parameters[0].ParameterType.Should().Be(typeof(EmailMessage));
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken));
        method.ReturnType.Should().Be(typeof(Task<SendResult>),
            because: "SendAsync retorna Task<SendResult> (design §8.1)");
    }

    // -------------------------------------------------------------------------
    // ToString() sem PII (TASK-06/ST-03, RNF 4)
    // -------------------------------------------------------------------------

    /// <summary>
    /// SendResult.ToString() não deve expor PII nem credencial.
    /// Mapeia: TASK-06/ST-03, RNF 4.
    /// </summary>
    [Fact(DisplayName = "SendResult.ToString() não deve expor PII (RNF 4)")]
    public void SendResult_ToString_ShouldNotExposePii()
    {
        // Arrange
        var result = new SendResult(
            status: SendStatus.PermanentFailure,
            correlationId: SampleCorrelationId,
            provider: "postmark",
            attemptCount: 1,
            messageId: null,
            reason: new FailureReason(FailureCode.InvalidRecipient, "Destinatário inválido", IsRetriable: false));

        // Act
        var str = result.ToString();

        // Assert — garante que a mensagem de FailureReason não contamina ToString de SendResult com PII acidental
        str.Should().NotContain("@",
            because: "ToString() não deve conter endereço de e-mail (RNF 4)");
    }
}
