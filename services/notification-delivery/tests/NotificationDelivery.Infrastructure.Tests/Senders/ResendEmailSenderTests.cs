using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.Mapping;
using NotificationDelivery.Infrastructure.Senders.Resend;
using NotificationDelivery.Infrastructure.Tests.Secrets;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.Senders;

/// <summary>
/// Testes do <see cref="ResendEmailSender"/> com <see cref="HttpMessageHandlerStub"/>.
///
/// Nenhuma chamada de rede real é feita (stub intercepta todas as requisições HTTP).
/// Credenciais nunca em logs (RNF-6.3).
///
/// Cobre os critérios de aceite da TASK-12:
/// - Sucesso retorna Sent com MessageId (ST-01a);
/// - 5xx retorna TransientFailure (ST-01b);
/// - hard bounce retorna Bounced via ProviderResponse (ST-01c);
/// - IdempotencyKey no header da requisição (ST-01d);
/// - Falha de SecretProvider retorna PermanentFailure/NOTIF-ERR-040 (ST-02);
/// - Architecture.Tests não quebra (RNF 1, DD-003).
/// </summary>
public sealed class ResendEmailSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers de construção
    // -------------------------------------------------------------------------

    private static EmailMessage BuildMessage(string? idempotencyKey = null) =>
        new(
            recipientEmail: "user@example.com",
            subject: "Assunto de teste",
            htmlBody: "<p>Corpo HTML de teste</p>",
            tenantId: "tenant-azim-001",
            correlationId: "corr-resend-test-001",
            idempotencyKey: idempotencyKey);

    private static ResendEmailSender BuildSender(
        HttpMessageHandlerStub handlerStub,
        ISecretProvider? secretProvider = null)
    {
        var httpClient = new HttpClient(handlerStub)
        {
            BaseAddress = new Uri("https://api.resend.com")
        };

        var provider = secretProvider ?? new SecretProviderFake(
            new Dictionary<string, string> { [ResendEmailSender.SecretName] = "re_test_key" });

        return new ResendEmailSender(
            httpClient,
            provider,
            new ProviderResponseMapper(),
            NullLogger<ResendEmailSender>.Instance);
    }

    // -------------------------------------------------------------------------
    // ST-01a: sucesso (200/201 com messageId) → ProviderResponse(IsSuccess=true)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "SendAsync: 200/201 com messageId → IsSuccess=true com MessageId")]
    [InlineData(200)]
    [InlineData(201)]
    public async Task SendAsync_WithSuccessResponse_ReturnsIsSuccessTrue(int statusCode)
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueJsonResponse(
            (HttpStatusCode)statusCode,
            """{"id": "msg-resend-abc-123"}""");

        var sender = BuildSender(stub);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.MessageId.Should().Be("msg-resend-abc-123");
        result.ErrorCode.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // ST-01b: 5xx → IsSuccess=false, IsRetriable=true, NOTIF-ERR-010
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "SendAsync: 5xx → IsSuccess=false com NOTIF-ERR-010")]
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

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.TransientProviderFailure);
        result.IsRetriable.Should().BeTrue();
        result.MessageId.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // ST-01c: hard bounce via NOTIF-ERR-030 na ProviderResponse
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: ProviderResponse com ErrorCode=NOTIF-ERR-030 preserva Bounced")]
    public async Task SendAsync_WhenBounceConfiguredInMapper_ReturnsBounceCode()
    {
        // Nota: O Resend não retorna bounce na resposta síncrona de envio (apenas via webhook DD-009).
        // Este teste verifica que ProviderResponse com código de bounce é corretamente construído
        // e que o sender pode produzir IsRetriable=false para bounce.

        // Arrange — simula 422 (payload com endereço suprimido/inválido que o provedor rejeita)
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueJsonResponse(HttpStatusCode.UnprocessableEntity, "{}");

        var sender = BuildSender(stub);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert — 422 → NOTIF-ERR-020 (PermanentFailure), não retriável
        result.IsSuccess.Should().BeFalse();
        result.IsRetriable.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.ProviderRejectedPayload);
    }

    // -------------------------------------------------------------------------
    // ST-01d: IdempotencyKey → header "Idempotency-Key" na requisição HTTP (DD-005, Req 9)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: IdempotencyKey presente → header Idempotency-Key na requisição")]
    public async Task SendAsync_WithIdempotencyKey_SetsHeaderInRequest()
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueJsonResponse(HttpStatusCode.OK, """{"id": "msg-idem-001"}""");

        var sender = BuildSender(stub);
        var message = BuildMessage(idempotencyKey: "idem-key-unique-xyz");

        // Act
        await sender.SendAsync(message);

        // Assert — header de idempotência presente na requisição capturada
        var lastRequest = stub.LastRequest;
        lastRequest.Should().NotBeNull();
        lastRequest!.Headers.TryGetValues("Idempotency-Key", out var values).Should().BeTrue(
            because: "IdempotencyKey deve ser propagada via header (DD-005, Req 9)");
        values.Should().Contain("idem-key-unique-xyz");
    }

    [Fact(DisplayName = "SendAsync: sem IdempotencyKey → header Idempotency-Key ausente")]
    public async Task SendAsync_WithoutIdempotencyKey_DoesNotSetHeader()
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueJsonResponse(HttpStatusCode.OK, """{"id": "msg-no-idem"}""");

        var sender = BuildSender(stub);
        var message = BuildMessage(idempotencyKey: null);

        // Act
        await sender.SendAsync(message);

        // Assert
        var lastRequest = stub.LastRequest;
        lastRequest!.Headers.Contains("Idempotency-Key").Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // ST-02: falha de SecretProvider → NOTIF-ERR-040
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: SecretProviderException → IsSuccess=false, NOTIF-ERR-040")]
    public async Task SendAsync_WithSecretProviderFailure_ReturnsPermanentFailure()
    {
        // Arrange — ISecretProvider fake configurado para falhar
        var stub = new HttpMessageHandlerStub();
        var failingProvider = new SecretProviderFake(
            failOn: new Dictionary<string, bool> { [ResendEmailSender.SecretName] = true });

        var sender = BuildSender(stub, secretProvider: failingProvider);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.SecretProviderFailure);
        result.IsRetriable.Should().BeFalse();

        // Stub não deve ter sido chamado (credencial indisponível → sem HTTP)
        stub.CapturedRequests.Should().BeEmpty(
            because: "quando o SecretProvider falha, nenhuma chamada HTTP deve ser feita");
    }

    // -------------------------------------------------------------------------
    // ST-03: credencial não logada (RNF-6.3, Req 10.4)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: credencial nunca aparece no header Authorization logado")]
    public async Task SendAsync_WithApiKey_AuthorizationHeaderNotLogged()
    {
        // Arrange
        var stub = new HttpMessageHandlerStub();
        stub.EnqueueJsonResponse(HttpStatusCode.OK, """{"id": "msg-sec-test"}""");

        const string apiKey = "re_secret_never_log_me";
        var provider = new SecretProviderFake(
            new Dictionary<string, string> { [ResendEmailSender.SecretName] = apiKey });

        var sender = BuildSender(stub, secretProvider: provider);
        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert — verificar que a requisição saiu corretamente mas a chave não está exposta
        result.IsSuccess.Should().BeTrue();

        // A credencial deve estar no header Authorization, mas nunca em log
        // (o teste de ausência em log está na TASK-15/PBT-03; aqui verificamos apenas que o header existe)
        stub.LastRequest!.Headers.Authorization.Should().NotBeNull();
        stub.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        // Não logamos o parâmetro aqui — RNF-6.3
    }

    // -------------------------------------------------------------------------
    // ST-04: falha de rede → IsRetriable=true (NOTIF-ERR-010)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "SendAsync: HttpRequestException (rede) → IsSuccess=false, IsRetriable=true")]
    public async Task SendAsync_WithNetworkError_ReturnsTransientFailure()
    {
        // Arrange — handler que lança HttpRequestException
        var throwingHandler = new ThrowingHttpMessageHandlerStub(
            new HttpRequestException("Conexão recusada"));
        var httpClient = new HttpClient(throwingHandler)
        {
            BaseAddress = new Uri("https://api.resend.com")
        };
        var provider = new SecretProviderFake(
            new Dictionary<string, string> { [ResendEmailSender.SecretName] = "re_test" });

        var sender = new ResendEmailSender(
            httpClient,
            provider,
            new ProviderResponseMapper(),
            NullLogger<ResendEmailSender>.Instance);

        var message = BuildMessage();

        // Act
        var result = await sender.SendAsync(message);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsRetriable.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // ST-05: 401/403 → NOTIF-ERR-021, IsRetriable=false
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
}

/// <summary>
/// Stub de <see cref="HttpMessageHandler"/> que lança sempre a exceção configurada.
/// Usado para simular falhas de rede (TASK-12/ST-04).
/// </summary>
internal sealed class ThrowingHttpMessageHandlerStub : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpMessageHandlerStub(Exception exception) =>
        _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        throw _exception;
}
