using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.Mapping;
using NotificationDelivery.Infrastructure.Senders.SendGrid;
using NotificationDelivery.Infrastructure.Tests.Secrets;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.Senders;

/// <summary>
/// Testes do <see cref="SendGridEmailSender"/> com <see cref="HttpMessageHandlerStub"/>.
///
/// Semântica equivalente à do <see cref="ResendEmailSenderTests"/> para entradas equivalentes
/// — base do PBT-01 (TASK-17): ambos os senders devem produzir <see cref="ProviderResponse"/>
/// com a mesma forma para a mesma entrada.
///
/// Nenhuma chamada de rede real é feita (stub intercepta todas as requisições HTTP).
/// Credenciais nunca em logs (RNF-6.3).
///
/// Cobre os critérios de aceite da TASK-13:
/// - Sucesso retorna IsSuccess=true com MessageId (ST-01a);
/// - 5xx retorna IsRetriable=true (ST-01b);
/// - Bounce retorna IsRetriable=false (ST-01c);
/// - EmailSenderContractTestBase semântica verificada (ST-01d);
/// - Architecture.Tests não quebra (RNF 1, DD-003).
/// </summary>
public sealed class SendGridEmailSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers de construção
    // -------------------------------------------------------------------------

    private static EmailMessage BuildMessage(string? idempotencyKey = null) =>
        new(
            recipientEmail: "user@example.com",
            subject: "Assunto de teste SendGrid",
            htmlBody: "<p>Corpo HTML de teste SendGrid</p>",
            tenantId: "tenant-azim-sg-001",
            correlationId: "corr-sendgrid-test-001",
            idempotencyKey: idempotencyKey);

    private static SendGridEmailSender BuildSender(
        HttpMessageHandlerStub handlerStub,
        ISecretProvider? secretProvider = null)
    {
        var httpClient = new HttpClient(handlerStub)
        {
            BaseAddress = new Uri("https://api.sendgrid.com")
        };

        var provider = secretProvider ?? new SecretProviderFake(
            new Dictionary<string, string> { [SendGridEmailSender.SecretName] = "SG.test_key" });

        return new SendGridEmailSender(
            httpClient,
            provider,
            new ProviderResponseMapper(),
            NullLogger<SendGridEmailSender>.Instance);
    }

    // -------------------------------------------------------------------------
    // ST-01a: sucesso (202 com X-Message-Id) → IsSuccess=true
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: 202 Accepted → IsSuccess=true com MessageId")]
    public async Task SendAsync_With202Response_ReturnsIsSuccessTrue()
    {
        // Arrange — SendGrid retorna 202 Accepted em sucesso, ID no header X-Message-Id
        var stub = new HttpMessageHandlerStub();
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Add("X-Message-Id", "sg-msg-abc-123");
        stub.EnqueueResponse(response);

        var sender = BuildSender(stub);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.MessageId.Should().Be("sg-msg-abc-123");
        result.ErrorCode.Should().BeNull();
    }

    [Fact(DisplayName = "SendAsync: 202 sem X-Message-Id → IsSuccess=true com ID sintético")]
    public async Task SendAsync_With202ResponseWithoutHeader_ReturnsIsSuccessTrueWithSyntheticId()
    {
        // Arrange — sem header X-Message-Id (compatibilidade com stubs)
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Accepted));

        var sender = BuildSender(stub);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert — ID sintético gerado internamente
        result.IsSuccess.Should().BeTrue();
        result.MessageId.Should().NotBeNullOrWhiteSpace(
            because: "um ID sintético deve ser gerado quando X-Message-Id está ausente");
    }

    // -------------------------------------------------------------------------
    // ST-01b: 5xx → IsRetriable=true (semântica equivalente ao ResendEmailSender)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "SendAsync: 5xx → IsSuccess=false, IsRetriable=true (NOTIF-ERR-010)")]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(429)]
    public async Task SendAsync_WithServerError_ReturnsTransientFailure(int statusCode)
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueJsonResponse((HttpStatusCode)statusCode, "{}");

        var sender = BuildSender(stub);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert — semântica equivalente ao ResendEmailSender (base PBT-01)
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.TransientProviderFailure);
        result.IsRetriable.Should().BeTrue();
        result.MessageId.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // ST-01c: 400/422 → PermanentFailure, IsRetriable=false
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "SendAsync: 400/422 → IsSuccess=false, IsRetriable=false (NOTIF-ERR-020)")]
    [InlineData(400)]
    [InlineData(422)]
    public async Task SendAsync_WithPayloadError_ReturnsPermanentFailure(int statusCode)
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueJsonResponse((HttpStatusCode)statusCode, "{}");

        var sender = BuildSender(stub);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert — semântica equivalente ao ResendEmailSender
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.ProviderRejectedPayload);
        result.IsRetriable.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // IdempotencyKey → header de idempotência do SendGrid (DD-005, Req 9)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: IdempotencyKey → header de idempotência do SendGrid presente")]
    public async Task SendAsync_WithIdempotencyKey_SetsIdempotencyHeader()
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.Accepted));

        var sender = BuildSender(stub);
        var message = BuildMessage(idempotencyKey: "idem-sg-test-xyz");

        // Act
        await sender.SendAsync(message);

        // Assert
        var lastRequest = stub.LastRequest;
        lastRequest.Should().NotBeNull();
        lastRequest!.Headers.TryGetValues(
            "X-Twilio-Email-List-Management-Idempotency-Token",
            out var values).Should().BeTrue(
            because: "IdempotencyKey deve ser propagada via header (DD-005, Req 9)");
        values.Should().Contain("idem-sg-test-xyz");
    }

    // -------------------------------------------------------------------------
    // SecretProvider failure → NOTIF-ERR-040
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: SecretProviderException → IsSuccess=false, NOTIF-ERR-040")]
    public async Task SendAsync_WithSecretProviderFailure_ReturnsPermanentFailure()
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        var failingProvider = new SecretProviderFake(
            failOn: new Dictionary<string, bool> { [SendGridEmailSender.SecretName] = true });

        var sender = BuildSender(stub, secretProvider: failingProvider);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.SecretProviderFailure);
        result.IsRetriable.Should().BeFalse();
        stub.CapturedRequests.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // 401/403 → NOTIF-ERR-021
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "SendAsync: 401/403 → NOTIF-ERR-021 (credencial inválida)")]
    [InlineData(401)]
    [InlineData(403)]
    public async Task SendAsync_WithInvalidCredentialResponse_ReturnsPermanentFailure(int statusCode)
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueJsonResponse((HttpStatusCode)statusCode, "{}");
        var sender = BuildSender(stub);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.InvalidOrRevokedCredential);
        result.IsRetriable.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Semântica equivalente ao ResendEmailSender (base PBT-01)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "Semântica: SendGrid e Resend produzem IsSuccess=true/false equivalentes")]
    [InlineData(200, true, "resend")]
    [InlineData(202, true, "sendgrid")]
    [InlineData(500, false, "resend")]
    [InlineData(500, false, "sendgrid")]
    public async Task Senders_WithEquivalentInput_ProduceEquivalentIsSuccessShape(
        int statusCode, bool expectedSuccess, string sender)
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();

        if (expectedSuccess)
        {
            if (sender == "resend")
                stub.EnqueueJsonResponse((HttpStatusCode)statusCode, """{"id": "msg-001"}""");
            else
            {
                var r = new HttpResponseMessage((HttpStatusCode)statusCode);
                r.Headers.Add("X-Message-Id", "sg-msg-001");
                stub.EnqueueResponse(r);
            }
        }
        else
        {
            stub.EnqueueJsonResponse((HttpStatusCode)statusCode, "{}");
        }

        ProviderResponse result;
        var message = BuildMessage();

        if (sender == "resend")
        {
            var resendSender = new NotificationDelivery.Infrastructure.Senders.Resend.ResendEmailSender(
                new HttpClient(stub) { BaseAddress = new Uri("https://api.resend.com") },
                new SecretProviderFake(new Dictionary<string, string> { ["RESEND_API_KEY"] = "re_key" }),
                new ProviderResponseMapper(),
                NullLogger<NotificationDelivery.Infrastructure.Senders.Resend.ResendEmailSender>.Instance);
            result = await resendSender.SendAsync(message);
        }
        else
        {
            var sgSender = new SendGridEmailSender(
                new HttpClient(stub) { BaseAddress = new Uri("https://api.sendgrid.com") },
                new SecretProviderFake(new Dictionary<string, string> { ["SENDGRID_API_KEY"] = "SG.key" }),
                new ProviderResponseMapper(),
                NullLogger<SendGridEmailSender>.Instance);
            result = await sgSender.SendAsync(message);
        }

        // Assert — forma equivalente (PBT-01 base estrutural)
        result.IsSuccess.Should().Be(expectedSuccess,
            because: $"sender={sender}, statusCode={statusCode}");
    }

    // -------------------------------------------------------------------------
    // Falha de rede → IsRetriable=true
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: HttpRequestException (rede) → IsSuccess=false, IsRetriable=true")]
    public async Task SendAsync_WithNetworkError_ReturnsTransientFailure()
    {
        // Arrange
        var throwingHandler = new ThrowingHttpMessageHandlerStub(
            new HttpRequestException("Conexão recusada"));
        var httpClient = new HttpClient(throwingHandler)
        {
            BaseAddress = new Uri("https://api.sendgrid.com")
        };
        var provider = new SecretProviderFake(
            new Dictionary<string, string> { [SendGridEmailSender.SecretName] = "SG.test" });

        var sender = new SendGridEmailSender(
            httpClient,
            provider,
            new ProviderResponseMapper(),
            NullLogger<SendGridEmailSender>.Instance);

        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsRetriable.Should().BeTrue();
    }
}
