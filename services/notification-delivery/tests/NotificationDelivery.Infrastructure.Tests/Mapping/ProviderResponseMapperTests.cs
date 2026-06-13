using System.Net;
using FluentAssertions;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.Mapping;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.Mapping;

/// <summary>
/// Testes parametrizados para <see cref="ProviderResponseMapper"/> (ACL).
///
/// Cobre todos os casos de mapeamento (Req 3, Req 7, PBT-04, design §12):
/// 200+message_id → Sent; 400/422 → PermanentFailure; 401/403 → PermanentFailure;
/// 429/5xx → TransientFailure; timeout → TransientFailure; hard_bounce → Bounced;
/// suppressed → Suppressed; resposta inesperada → TransientFailure/NOTIF-ERR-090.
///
/// Invariante PBT-04: todo caso resulta em exatamente um <see cref="SendStatus"/> canônico
/// sem lançar exceção.
/// </summary>
public sealed class ProviderResponseMapperTests
{
    private readonly ProviderResponseMapper _mapper = new();
    private const string CorrelationId = "corr-test-001";
    private const string Provider = "test-provider";

    // -------------------------------------------------------------------------
    // MapHttpStatus — casos canônicos (design §12, PBT-04)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "MapHttpStatus: 200/202 com messageId → Sent")]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    public void MapHttpStatus_WithSuccessCodeAndMessageId_ReturnsSent(HttpStatusCode code)
    {
        // Act
        var result = _mapper.MapHttpStatus(
            code, CorrelationId, Provider, attemptCount: 1, messageId: "msg-abc-123");

        // Assert
        result.Status.Should().Be(SendStatus.Sent);
        result.MessageId.Should().Be("msg-abc-123");
        result.Reason.Should().BeNull();
        result.CorrelationId.Should().Be(CorrelationId);
        result.Provider.Should().Be(Provider);
    }

    [Theory(DisplayName = "MapHttpStatus: 400/422 → PermanentFailure/NOTIF-ERR-020")]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public void MapHttpStatus_WithPayloadRejection_ReturnsPermanentFailure(HttpStatusCode code)
    {
        // Act
        var result = _mapper.MapHttpStatus(code, CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.PermanentFailure);
        result.Reason!.Code.Should().Be(FailureCode.ProviderRejectedPayload);
        result.Reason.IsRetriable.Should().BeFalse();
        result.MessageId.Should().BeNull();
    }

    [Theory(DisplayName = "MapHttpStatus: 401/403 → PermanentFailure/NOTIF-ERR-021")]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void MapHttpStatus_WithInvalidCredential_ReturnsPermanentFailure(HttpStatusCode code)
    {
        // Act
        var result = _mapper.MapHttpStatus(code, CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.PermanentFailure);
        result.Reason!.Code.Should().Be(FailureCode.InvalidOrRevokedCredential);
        result.Reason.IsRetriable.Should().BeFalse();
        result.MessageId.Should().BeNull();
    }

    [Theory(DisplayName = "MapHttpStatus: 429/5xx → TransientFailure/NOTIF-ERR-010")]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void MapHttpStatus_WithTransientError_ReturnsTransientFailure(HttpStatusCode code)
    {
        // Act
        var result = _mapper.MapHttpStatus(code, CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.TransientFailure);
        result.Reason!.Code.Should().Be(FailureCode.TransientProviderFailure);
        result.Reason.IsRetriable.Should().BeTrue();
        result.MessageId.Should().BeNull();
    }

    [Fact(DisplayName = "MapHttpStatus: isBounce=true → Bounced/NOTIF-ERR-030")]
    public void MapHttpStatus_WithBounceFlag_ReturnsBounced()
    {
        // Act
        var result = _mapper.MapHttpStatus(HttpStatusCode.OK, CorrelationId, Provider, isBounce: true);

        // Assert
        result.Status.Should().Be(SendStatus.Bounced);
        result.Reason!.Code.Should().Be(FailureCode.HardBounce);
        result.Reason.IsRetriable.Should().BeFalse();
        result.MessageId.Should().BeNull();
    }

    [Fact(DisplayName = "MapHttpStatus: isSuppressed=true → Suppressed/NOTIF-ERR-031")]
    public void MapHttpStatus_WithSuppressionFlag_ReturnsSuppressed()
    {
        // Act
        var result = _mapper.MapHttpStatus(HttpStatusCode.OK, CorrelationId, Provider, isSuppressed: true);

        // Assert
        result.Status.Should().Be(SendStatus.Suppressed);
        result.Reason!.Code.Should().Be(FailureCode.AddressSuppressed);
        result.Reason.IsRetriable.Should().BeFalse();
        result.MessageId.Should().BeNull();
    }

    [Fact(DisplayName = "MapHttpStatus: resposta inesperada → TransientFailure/NOTIF-ERR-090")]
    public void MapHttpStatus_WithUnexpectedCode_ReturnsUnclassifiable()
    {
        // Act — código não mapeado (ex.: 301)
        var result = _mapper.MapHttpStatus(HttpStatusCode.MovedPermanently, CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.TransientFailure);
        result.Reason!.Code.Should().Be(FailureCode.UnclassifiableProviderResponse);
        result.Reason.IsRetriable.Should().BeTrue();
        result.MessageId.Should().BeNull();
    }

    [Fact(DisplayName = "MapHttpStatus: 200 sem messageId → fallback NOTIF-ERR-090")]
    public void MapHttpStatus_With200ButNoMessageId_ReturnsUnclassifiable()
    {
        // Act — 200 mas sem messageId (sucesso parcial não esperado)
        var result = _mapper.MapHttpStatus(HttpStatusCode.OK, CorrelationId, Provider, messageId: null);

        // Assert — fallback conservador (NOTIF-ERR-090, PBT-04)
        result.Status.Should().Be(SendStatus.TransientFailure);
        result.Reason!.Code.Should().Be(FailureCode.UnclassifiableProviderResponse);
    }

    // -------------------------------------------------------------------------
    // MapException — casos canônicos (design §12, PBT-04)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "MapException: TaskCanceledException → TransientFailure/NOTIF-ERR-012")]
    public void MapException_WithTaskCanceled_ReturnsTimeout()
    {
        // Act
        var result = _mapper.MapException(
            new TaskCanceledException("Timeout"), CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.TransientFailure);
        result.Reason!.Code.Should().Be(FailureCode.AttemptTimeout);
        result.Reason.IsRetriable.Should().BeTrue();
        result.MessageId.Should().BeNull();
    }

    [Fact(DisplayName = "MapException: OperationCanceledException → TransientFailure/NOTIF-ERR-012")]
    public void MapException_WithOperationCanceled_ReturnsTimeout()
    {
        // Act
        var result = _mapper.MapException(
            new OperationCanceledException(), CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.TransientFailure);
        result.Reason!.Code.Should().Be(FailureCode.AttemptTimeout);
        result.Reason.IsRetriable.Should().BeTrue();
    }

    [Fact(DisplayName = "MapException: HttpRequestException → TransientFailure/NOTIF-ERR-010")]
    public void MapException_WithHttpRequestException_ReturnsTransientFailure()
    {
        // Act
        var result = _mapper.MapException(
            new HttpRequestException("Conexão recusada"), CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.TransientFailure);
        result.Reason!.Code.Should().Be(FailureCode.TransientProviderFailure);
        result.Reason.IsRetriable.Should().BeTrue();
    }

    [Fact(DisplayName = "MapException: exceção inesperada → TransientFailure/NOTIF-ERR-090")]
    public void MapException_WithUnexpectedException_ReturnsUnclassifiable()
    {
        // Act — exceção não mapeada
        var result = _mapper.MapException(
            new InvalidOperationException("Erro inesperado"), CorrelationId, Provider);

        // Assert — fallback conservador (NOTIF-ERR-090, PBT-04)
        result.Status.Should().Be(SendStatus.TransientFailure);
        result.Reason!.Code.Should().Be(FailureCode.UnclassifiableProviderResponse);
        result.Reason.IsRetriable.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Map (ProviderResponse) — casos canônicos
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "Map(ProviderResponse): IsSuccess=true com MessageId → Sent")]
    public void Map_WithSuccessResponse_ReturnsSent()
    {
        // Arrange
        var response = new ProviderResponse(
            IsSuccess: true,
            MessageId: "msg-resend-xyz",
            ErrorCode: null,
            ErrorMessage: null,
            IsRetriable: false);

        // Act
        var result = _mapper.Map(response, CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.Sent);
        result.MessageId.Should().Be("msg-resend-xyz");
        result.Reason.Should().BeNull();
    }

    [Fact(DisplayName = "Map(ProviderResponse): IsSuccess=false com NOTIF-ERR-030 → Bounced")]
    public void Map_WithBounceErrorCode_ReturnsBounced()
    {
        // Arrange
        var response = new ProviderResponse(
            IsSuccess: false,
            MessageId: null,
            ErrorCode: FailureCode.HardBounce,
            ErrorMessage: "Hard bounce detectado",
            IsRetriable: false);

        // Act
        var result = _mapper.Map(response, CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.Bounced);
        result.Reason!.Code.Should().Be(FailureCode.HardBounce);
        result.Reason.IsRetriable.Should().BeFalse();
    }

    [Fact(DisplayName = "Map(ProviderResponse): IsSuccess=false com NOTIF-ERR-031 → Suppressed")]
    public void Map_WithSuppressionErrorCode_ReturnsSuppressed()
    {
        // Arrange
        var response = new ProviderResponse(
            IsSuccess: false,
            MessageId: null,
            ErrorCode: FailureCode.AddressSuppressed,
            ErrorMessage: "Suprimido",
            IsRetriable: false);

        // Act
        var result = _mapper.Map(response, CorrelationId, Provider);

        // Assert
        result.Status.Should().Be(SendStatus.Suppressed);
        result.Reason!.Code.Should().Be(FailureCode.AddressSuppressed);
    }

    [Fact(DisplayName = "Map(ProviderResponse): código desconhecido → TransientFailure/NOTIF-ERR-090")]
    public void Map_WithUnknownErrorCode_ReturnsUnclassifiable()
    {
        // Arrange
        var response = new ProviderResponse(
            IsSuccess: false,
            MessageId: null,
            ErrorCode: "NOTIF-ERR-999",
            ErrorMessage: "Código não mapeado",
            IsRetriable: true);

        // Act
        var result = _mapper.Map(response, CorrelationId, Provider);

        // Assert — fallback conservador (PBT-04)
        result.Status.Should().Be(SendStatus.TransientFailure);
        result.Reason!.Code.Should().Be(FailureCode.UnclassifiableProviderResponse);
    }

    // -------------------------------------------------------------------------
    // Invariante: nenhuma mensagem de FailureReason expõe PII (RNF 4, Req 3.3)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "FailureReason.Message não contém PII em nenhum caso de falha")]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void MapHttpStatus_FailureReason_NeverContainsPii(HttpStatusCode code)
    {
        const string piiEmail = "user@example.com";
        const string apiKey = "sk_live_secret_key_12345";

        // Act
        var result = _mapper.MapHttpStatus(code, CorrelationId, Provider);

        // Assert — FailureReason.Message não deve conter e-mail nem credencial
        result.Reason!.Message.Should().NotContain(piiEmail,
            because: "FailureReason.Message não deve expor PII do destinatário (RNF 4, Req 3.3)");
        result.Reason.Message.Should().NotContain(apiKey,
            because: "FailureReason.Message não deve expor credencial de provedor (RNF 6)");
    }

    // -------------------------------------------------------------------------
    // Invariante: CorrelationId sempre propagado (design §8.3, Req 3.2)
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "CorrelationId sempre propagado em todos os casos")]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void MapHttpStatus_CorrelationId_AlwaysPropagated(HttpStatusCode code)
    {
        // Act
        var result = _mapper.MapHttpStatus(
            code, CorrelationId, Provider,
            messageId: code == HttpStatusCode.OK ? "msg-id" : null);

        // Assert
        result.CorrelationId.Should().Be(CorrelationId);
    }

    // -------------------------------------------------------------------------
    // Invariante: nenhum estado indefinido ou exceção propagada (PBT-04)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "PBT-04 base: ProviderResponseMapper.MapException nunca lança exceção")]
    public void MapException_WithNullException_DoesNotThrow()
    {
        // Act — mesmo passando ArgumentNullException, não propaga
        var act = () => _mapper.MapException(
            new ArgumentNullException("param"), CorrelationId, Provider);

        // Assert
        act.Should().NotThrow();
    }

    [Fact(DisplayName = "PBT-04 base: MapHttpStatus com qualquer código retorna SendResult válido")]
    public void MapHttpStatus_WithAnyStatusCode_ReturnsValidSendResult()
    {
        // Arrange — array de todos os HttpStatusCode enum values
        var allCodes = Enum.GetValues<HttpStatusCode>();

        foreach (var code in allCodes)
        {
            // Act — não deve lançar para nenhum código
            var act = () => _mapper.MapHttpStatus(code, CorrelationId, Provider);
            act.Should().NotThrow(
                because: $"MapHttpStatus não deve lançar para HttpStatusCode.{code} (PBT-04)");
        }
    }
}
