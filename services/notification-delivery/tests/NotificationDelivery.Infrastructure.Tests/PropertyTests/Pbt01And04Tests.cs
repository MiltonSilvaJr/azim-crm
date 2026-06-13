using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationDelivery.Application.Ports;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.Mapping;
using NotificationDelivery.Infrastructure.Senders.Resend;
using NotificationDelivery.Infrastructure.Senders.SendGrid;
using NotificationDelivery.Infrastructure.Tests.Secrets;
using NotificationDelivery.Infrastructure.Tests.Senders;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.PropertyTests;

/// <summary>
/// PBT-01: Reversibilidade do adapter — para qualquer <see cref="EmailMessage"/> válida,
/// <see cref="ResendEmailSender"/> e <see cref="SendGridEmailSender"/> produzem
/// <see cref="ProviderResponse"/> com a mesma forma e conjunto de estados canônicos.
///
/// PBT-04: Totalidade da classificação ACL — para qualquer resposta de provedor do conjunto
/// discreto definido em §13.1, <see cref="ProviderResponseMapper"/> produz exatamente um
/// <see cref="SendStatus"/> canônico sem lançar exceção.
///
/// ≥ 500 exemplos cada (requisito tasks.md TASK-17, requirements PBT-01/04).
///
/// Mapeia: TASK-17, PBT-01 (Req 1, Req 4, RNF 1), PBT-04 (Req 3, Req 7, design §4.5).
/// </summary>
public sealed class Pbt01And04Tests
{
    // -------------------------------------------------------------------------
    // Configuração: 500 exemplos por propriedade
    // Seed de reprodução registrado em comentário para debugging de shrinking.
    // Config.WithMaxTest(500): garante ≥ 500 exemplos.
    // -------------------------------------------------------------------------
    private static readonly Config FsCheckConfig = Config.QuickThrowOnFailure.WithMaxTest(500);

    // -------------------------------------------------------------------------
    // Factories de sender com stubs HTTP
    // -------------------------------------------------------------------------

    private static ResendEmailSender BuildResendSender(HttpMessageHandlerStub stub)
    {
        var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://api.resend.com") };
        var secretProvider = new SecretProviderFake(
            new Dictionary<string, string> { [ResendEmailSender.SecretName] = "re_pbt_test_key" });
        return new ResendEmailSender(
            httpClient, secretProvider, new ProviderResponseMapper(),
            NullLogger<ResendEmailSender>.Instance);
    }

    private static SendGridEmailSender BuildSendGridSender(HttpMessageHandlerStub stub)
    {
        var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://api.sendgrid.com") };
        var secretProvider = new SecretProviderFake(
            new Dictionary<string, string> { [SendGridEmailSender.SecretName] = "SG.pbt_test_key" });
        return new SendGridEmailSender(
            httpClient, secretProvider, new ProviderResponseMapper(),
            NullLogger<SendGridEmailSender>.Instance);
    }

    // -------------------------------------------------------------------------
    // PBT-01A: Resend e SendGrid produzem IsSuccess=true equivalente para sucesso
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-01 (parte A): para qualquer EmailMessage válida enviada com resposta de sucesso,
    /// ambos Resend (200) e SendGrid (202) retornam IsSuccess=true com MessageId presente.
    ///
    /// Forma equivalente: nenhum chamador distingue o provedor pelo tipo/estrutura do resultado
    /// (Req 4.3, PBT-01, RNF 1).
    /// </summary>
    [Fact(DisplayName = "PBT-01A: Resend e SendGrid retornam IsSuccess=true equivalente para sucesso (≥500 exemplos)")]
    public void PbtAdapterReversibility_SuccessCase_BothProduceSameShape()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailMessage(),
            message =>
            {
                // Resend: 200 com message_id
                var resendStub = new HttpMessageHandlerStub();
                resendStub.EnqueueJsonResponse(System.Net.HttpStatusCode.OK, """{"id": "msg-pbt-001"}""");

                // SendGrid: 202 com X-Message-Id
                var sgStub = new HttpMessageHandlerStub();
                var sgResp = new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                sgResp.Headers.Add("X-Message-Id", "sg-pbt-001");
                sgStub.EnqueueResponse(sgResp);

                var resendResult = BuildResendSender(resendStub).SendAsync(message).GetAwaiter().GetResult();
                var sgResult = BuildSendGridSender(sgStub).SendAsync(message).GetAwaiter().GetResult();

                // Propriedade: ambos IsSuccess=true
                resendResult.IsSuccess.Should().BeTrue($"Resend deve retornar sucesso. corr={message.CorrelationId}");
                sgResult.IsSuccess.Should().BeTrue($"SendGrid deve retornar sucesso. corr={message.CorrelationId}");

                // MessageId presente em ambos
                resendResult.MessageId.Should().NotBeNullOrWhiteSpace("Resend: MessageId obrigatório em sucesso");
                sgResult.MessageId.Should().NotBeNullOrWhiteSpace("SendGrid: MessageId obrigatório em sucesso");

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    /// <summary>
    /// PBT-01 (parte B): para qualquer EmailMessage válida enviada com falha transiente (5xx),
    /// ambos Resend e SendGrid retornam IsSuccess=false com IsRetriable=true.
    ///
    /// Semântica equivalente entre senders (Req 4.3, PBT-01).
    /// </summary>
    [Fact(DisplayName = "PBT-01B: Resend e SendGrid retornam IsSuccess=false equivalente para falha 5xx (≥500 exemplos)")]
    public void PbtAdapterReversibility_TransientFailureCase_BothProduceSameShape()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailMessage(),
            message =>
            {
                var resendStub = new HttpMessageHandlerStub();
                resendStub.EnqueueJsonResponse(System.Net.HttpStatusCode.InternalServerError, "{}");

                var sgStub = new HttpMessageHandlerStub();
                sgStub.EnqueueJsonResponse(System.Net.HttpStatusCode.InternalServerError, "{}");

                var resendResult = BuildResendSender(resendStub).SendAsync(message).GetAwaiter().GetResult();
                var sgResult = BuildSendGridSender(sgStub).SendAsync(message).GetAwaiter().GetResult();

                // Propriedade: ambos IsSuccess=false
                resendResult.IsSuccess.Should().BeFalse("Resend: 500 deve ser falha");
                sgResult.IsSuccess.Should().BeFalse("SendGrid: 500 deve ser falha");

                // Ambos retriáveis
                resendResult.IsRetriable.Should().BeTrue("Resend: 5xx é transiente (NOTIF-ERR-010)");
                sgResult.IsRetriable.Should().BeTrue("SendGrid: 5xx é transiente (NOTIF-ERR-010)");

                // Sem MessageId em falha
                resendResult.MessageId.Should().BeNull();
                sgResult.MessageId.Should().BeNull();

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    /// <summary>
    /// PBT-01 (parte C): para qualquer EmailMessage válida enviada com falha permanente (401),
    /// ambos Resend e SendGrid retornam IsRetriable=false.
    ///
    /// Invariante de falha permanente equivalente (PBT-01, Req 4.3).
    /// </summary>
    [Fact(DisplayName = "PBT-01C: Resend e SendGrid retornam IsRetriable=false equivalente para 401 (≥500 exemplos)")]
    public void PbtAdapterReversibility_PermanentFailureCase_BothProduceSameShape()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailMessage(),
            message =>
            {
                var resendStub = new HttpMessageHandlerStub();
                resendStub.EnqueueJsonResponse(System.Net.HttpStatusCode.Unauthorized, "{}");

                var sgStub = new HttpMessageHandlerStub();
                sgStub.EnqueueJsonResponse(System.Net.HttpStatusCode.Unauthorized, "{}");

                var resendResult = BuildResendSender(resendStub).SendAsync(message).GetAwaiter().GetResult();
                var sgResult = BuildSendGridSender(sgStub).SendAsync(message).GetAwaiter().GetResult();

                // Propriedade: ambos IsRetriable=false para falha permanente
                resendResult.IsSuccess.Should().BeFalse();
                sgResult.IsSuccess.Should().BeFalse();

                resendResult.IsRetriable.Should().BeFalse("401 é permanente no Resend");
                sgResult.IsRetriable.Should().BeFalse("401 é permanente no SendGrid");

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    // -------------------------------------------------------------------------
    // PBT-04: Totalidade da classificação ACL
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-04 (MapHttpStatus): para qualquer resposta do conjunto de 11 casos (design §13.1),
    /// <see cref="ProviderResponseMapper.MapHttpStatus"/> produz exatamente um <see cref="SendStatus"/>
    /// canônico sem lançar exceção — mapeamento total (PBT-04, Req 7, design §4.5).
    /// </summary>
    [Fact(DisplayName = "PBT-04A: ProviderResponseMapper.MapHttpStatus produz status definido para todos os casos (≥500 exemplos)")]
    public void PbtAclTotality_MapHttpStatus_ProducesDefinedStatusForAllCases()
    {
        var mapper = new ProviderResponseMapper();
        var validStatuses = Enum.GetValues<SendStatus>();

        var prop = Prop.ForAll(
            EmailMessageArbitraries.ProviderResponseCases(),
            EmailMessageArbitraries.ValidEmailMessage(),
            (responseCase, message) =>
            {
                // Act — nunca deve lançar (PBT-04)
                SendResult result;
                try
                {
                    result = mapper.MapHttpStatus(
                        statusCode: responseCase.StatusCode,
                        correlationId: message.CorrelationId,
                        provider: "pbt-provider",
                        attemptCount: 1,
                        messageId: responseCase.MessageId,
                        isBounce: responseCase.IsBounce,
                        isSuppressed: responseCase.IsSuppressed);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"PBT-04 falhou: MapHttpStatus lançou exceção para '{responseCase.Description}'. " +
                        $"{ex.GetType().Name}: {ex.Message}", ex);
                }

                // Status é canônico definido
                result.Should().NotBeNull($"MapHttpStatus nunca retorna null ('{responseCase.Description}')");
                result.Status.Should().BeOneOf(validStatuses,
                    $"Status canônico esperado ('{responseCase.Description}')");

                // CorrelationId sempre propagado
                result.CorrelationId.Should().Be(message.CorrelationId,
                    $"CorrelationId deve ser propagado ('{responseCase.Description}')");

                // Invariantes de SendResult (design §8.3)
                if (result.Status == SendStatus.Sent)
                {
                    result.MessageId.Should().NotBeNullOrWhiteSpace(
                        $"MessageId obrigatório quando Sent ('{responseCase.Description}')");
                    result.Reason.Should().BeNull(
                        $"Reason nulo quando Sent ('{responseCase.Description}')");
                }
                else
                {
                    result.MessageId.Should().BeNull(
                        $"MessageId nulo para falha ('{responseCase.Description}')");
                    result.Reason.Should().NotBeNull(
                        $"Reason obrigatório para falha ('{responseCase.Description}')");

                    // PII não aparece no FailureReason.Message (RNF 4)
                    result.Reason!.Message.Should().NotContain("@",
                        $"FailureReason.Message sem e-mail ('{responseCase.Description}', RNF 4)");
                }

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    /// <summary>
    /// PBT-04 (MapException): para qualquer exceção de infraestrutura,
    /// <see cref="ProviderResponseMapper.MapException"/> produz <see cref="SendResult"/>
    /// com status canônico sem relançar — fallback NOTIF-ERR-090 (PBT-04).
    /// </summary>
    [Fact(DisplayName = "PBT-04B: ProviderResponseMapper.MapException nunca propaga exceção (≥500 exemplos)")]
    public void PbtAclTotality_MapException_NeverThrows()
    {
        var mapper = new ProviderResponseMapper();
        var exceptions = new Exception[]
        {
            new HttpRequestException("Conexão recusada"),
            new TaskCanceledException("Timeout"),
            new OperationCanceledException("Cancelado"),
            new InvalidOperationException("Erro genérico"),
            new TimeoutException("Timeout de rede"),
            new IOException("Erro de IO"),
        };

        var prop = Prop.ForAll(
            Gen.Elements(exceptions).ToArbitrary(),
            EmailMessageArbitraries.ValidEmailMessage(),
            (exception, message) =>
            {
                // Act — nunca deve lançar (PBT-04)
                SendResult result;
                try
                {
                    result = mapper.MapException(
                        exception: exception,
                        correlationId: message.CorrelationId,
                        provider: "pbt-provider",
                        attemptCount: 1);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"PBT-04 falhou: MapException propagou {ex.GetType().Name}: {ex.Message}", ex);
                }

                // Resultado definido e sem estado indefinido
                result.Should().NotBeNull();
                result.Status.Should().NotBe(SendStatus.Sent, "exceções nunca mapeiam para Sent");
                result.Reason.Should().NotBeNull("exceções sempre produzem Reason");
                result.Reason!.Code.Should().NotBeNullOrWhiteSpace();
                result.CorrelationId.Should().Be(message.CorrelationId);

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }
}
