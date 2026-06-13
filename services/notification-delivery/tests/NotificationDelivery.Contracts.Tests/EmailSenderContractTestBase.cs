using FluentAssertions;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Contracts.Tests;

/// <summary>
/// Classe abstrata de teste de contrato compartilhada para qualquer implementação de <see cref="IEmailSender"/>.
///
/// Qualquer <c>IEmailSender</c> concreto (PostmarkEmailSender, SendGridEmailSender, ResendEmailSender)
/// deve satisfazer estas asserções de forma quando executado contra esta base (PBT-01 base estrutural).
///
/// Estrutura base para TASK-17 (PBT-01 — reversibilidade do adapter).
/// Mapeia: TASK-06/ST-02, Req 1, Req 3, Req 3.1..3.5, PBT-01, design §13.
///
/// Como usar:
/// <code>
/// public class PostmarkEmailSenderContractTests : EmailSenderContractTestBase
/// {
///     protected override IEmailSender CreateSender() => new PostmarkEmailSender(/* deps */);
/// }
/// </code>
/// </summary>
public abstract class EmailSenderContractTestBase
{
    // -------------------------------------------------------------------------
    // Hook abstrato — a ser implementado por cada suíte concreta
    // -------------------------------------------------------------------------

    /// <summary>
    /// Cria a instância de <see cref="IEmailSender"/> sob teste.
    /// Implementado por cada suíte concreta com a configuração necessária (ex.: sender com stub de HTTP).
    /// </summary>
    protected abstract IEmailSender CreateSender();

    // -------------------------------------------------------------------------
    // Helpers de fixture
    // -------------------------------------------------------------------------

    /// <summary>Cria uma <see cref="EmailMessage"/> válida para uso nos testes de contrato.</summary>
    protected static EmailMessage ValidEmailMessage(string? idempotencyKey = null) =>
        new EmailMessage(
            recipientEmail: "contrato@teste.azim.com.br",
            subject: "Teste de contrato — sender",
            htmlBody: "<p>Corpo HTML do teste de contrato.</p>",
            tenantId: "tenant-contrato-001",
            correlationId: Guid.NewGuid().ToString(),
            idempotencyKey: idempotencyKey);

    // -------------------------------------------------------------------------
    // Asserções de forma de SendResult (compartilhadas entre todos os senders)
    // Mapeia: PBT-01, Req 1, Req 3, design §8.3
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifica que um <see cref="SendResult"/> de sucesso está bem formado.
    /// <see cref="SendStatus.Sent"/>: MessageId presente, Reason nulo, CorrelationId propagado.
    /// </summary>
    protected static void AssertSentResultIsWellFormed(SendResult result, string expectedCorrelationId)
    {
        result.Should().NotBeNull(because: "SendAsync nunca retorna null (Req 3.5)");
        result.Status.Should().Be(SendStatus.Sent);
        result.MessageId.Should().NotBeNullOrWhiteSpace(
            because: "MessageId deve estar presente quando Status=Sent (design §8.3)");
        result.Reason.Should().BeNull(
            because: "Reason deve ser null quando Status=Sent (design §8.3)");
        result.CorrelationId.Should().Be(expectedCorrelationId,
            because: "CorrelationId deve ser ecoado da mensagem de origem (design §8.3, Req 3.2)");
    }

    /// <summary>
    /// Verifica que um <see cref="SendResult"/> de falha está bem formado.
    /// Qualquer status de falha: MessageId nulo, Reason presente (com Code do catálogo), CorrelationId propagado.
    /// </summary>
    protected static void AssertFailureResultIsWellFormed(SendResult result, string expectedCorrelationId)
    {
        result.Should().NotBeNull(because: "SendAsync nunca retorna null (Req 3.5)");
        result.Status.Should().NotBe(SendStatus.Sent,
            because: "este helper é para resultados de falha");
        result.MessageId.Should().BeNull(
            because: "MessageId deve ser null quando Status não é Sent (design §8.3)");
        result.Reason.Should().NotBeNull(
            because: "Reason deve estar presente quando Status não é Sent (design §8.3)");
        result.Reason!.Code.Should().NotBeNullOrWhiteSpace(
            because: "Code deve pertencer ao catálogo FailureCode (design §12)");
        result.CorrelationId.Should().Be(expectedCorrelationId,
            because: "CorrelationId deve ser ecoado da mensagem de origem em todos os casos (design §8.3, Req 3.2)");
        result.Reason.Message.Should().NotContain("@",
            because: "FailureReason.Message não deve expor e-mail do destinatário (RNF 4, Req 3.3)");
    }

    /// <summary>
    /// Verifica que o <see cref="SendResult"/> tem forma válida independentemente do status.
    /// Aplicável em PBT-01 onde o status pode variar, mas a forma deve ser consistente.
    /// </summary>
    protected static void AssertResultHasValidShape(SendResult result, string expectedCorrelationId)
    {
        result.Should().NotBeNull(because: "SendAsync nunca retorna null (Req 3.5)");
        result.CorrelationId.Should().Be(expectedCorrelationId,
            because: "CorrelationId sempre propagado (design §8.3, Req 3.2)");

        if (result.Status == SendStatus.Sent)
        {
            AssertSentResultIsWellFormed(result, expectedCorrelationId);
        }
        else
        {
            AssertFailureResultIsWellFormed(result, expectedCorrelationId);
        }
    }

    // -------------------------------------------------------------------------
    // Testes de contrato estruturais (suíte vazia até TASK-17 populá-la com PBT-01)
    // Mapeia: TASK-06 criterio "EmailSenderContractTestBase compilando (suíte vazia até TASK-17)"
    // -------------------------------------------------------------------------

    // NOTA: os testes concretos de PBT-01 serão adicionados em TASK-17 (Onda 5).
    // Esta classe compila e pode ser herdada por qualquer suíte de contrato de sender.
    // Os helpers AssertSentResultIsWellFormed / AssertFailureResultIsWellFormed / AssertResultHasValidShape
    // encapsulam as asserções de forma que qualquer PBT de sender deve satisfazer.
}
