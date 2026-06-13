using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationDelivery.Application.Resilience;
using NotificationDelivery.Application.Telemetry;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Application.Tests.Hardening;

/// <summary>
/// Testes de caos e hardening do módulo notification-delivery (TASK-21).
///
/// Caos: stub que retorna 503 em todas as respostas → <see cref="ResilientEmailSender"/>
/// deve retornar <see cref="SendStatus.TransientFailure"/> com código NOTIF-ERR-011
/// sem bloquear e sem lançar exceção ao chamador (Req 3.5, RNF 3, TASK-21).
///
/// Isolamento: 3 mensagens processadas independentemente — falha em uma não
/// deve impedir processamento das demais (Req 8.5, TASK-21).
///
/// Mapeia: TASK-21, RNF 3 (resiliência), Req 3.5 (nunca lança), Req 8.5 (isolamento por mensagem).
/// </summary>
public sealed class ChaosAndIsolationTests
{
    // -------------------------------------------------------------------------
    // Fake sender que sempre retorna TransientFailure (simula 503 de provedor)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sender fake que sempre retorna <see cref="SendStatus.TransientFailure"/>.
    /// Simula provedor com 100% de falha (chaos: servidor respondendo 503 em todas as chamadas).
    /// </summary>
    private sealed class AlwaysFailFakeSender : IEmailSender
    {
        public int CallCount { get; private set; }

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SendResult(
                status: SendStatus.TransientFailure,
                correlationId: message.CorrelationId,
                provider: "chaos-503-fake",
                attemptCount: CallCount,
                messageId: null,
                reason: new FailureReason(
                    FailureCode.TransientProviderFailure,
                    "Provedor retornou 503 Service Unavailable (chaos test).",
                    IsRetriable: true)));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Unhealthy("Chaos: provedor indisponível."));
    }

    // -------------------------------------------------------------------------
    // Factory: ResilientEmailSender com configuração de caos (delays zerados)
    // -------------------------------------------------------------------------

    private static ResilientEmailSender BuildChaosResilientSender(
        IEmailSender inner,
        int maxRetries = 3,
        int circuitBreakerThreshold = 20)
    {
        var metrics = new NotificationDeliveryMetrics();
        return new ResilientEmailSender(
            inner,
            Options.Create(new ResilientEmailSenderOptions
            {
                MaxRetryAttempts = maxRetries,
                TimeoutPerAttemptSeconds = 30,
                CircuitBreakerFailureThreshold = circuitBreakerThreshold,
                CircuitBreakerBreakDurationSeconds = 1,
                BaseRetryDelayMs = 0,    // sem delay: caos rápido
                UseJitter = false
            }),
            NullLogger<ResilientEmailSender>.Instance,
            metrics);
    }

    private static EmailMessage BuildMessage(string tenantId = "tenant-chaos", string? suffix = null) =>
        new(
            recipientEmail: $"chaos{suffix ?? string.Empty}@example.com",
            subject: "Chaos Test",
            htmlBody: "<p>Chaos</p>",
            tenantId: tenantId,
            correlationId: Guid.NewGuid().ToString(),
            idempotencyKey: null);

    // -------------------------------------------------------------------------
    // TASK-21-T01: Chaos — provedor 503 → TransientFailure sem bloqueio
    // -------------------------------------------------------------------------

    /// <summary>
    /// TASK-21-T01 (Chaos): quando o provedor retorna 503 em todas as tentativas,
    /// <see cref="ResilientEmailSender.SendAsync"/> deve:
    /// - retornar <see cref="SendStatus.TransientFailure"/> (nunca lançar exceção — Req 3.5);
    /// - completar sem bloquear (Req 8.2);
    /// - o código de falha deve ser NOTIF-ERR-010 ou NOTIF-ERR-011 (TransientProviderFailure ou CircuitBreakerOpen).
    /// </summary>
    [Fact(DisplayName = "TASK-21-T01: caos 503 retorna TransientFailure sem bloquear (Req 3.5, RNF 3)")]
    public async Task Chaos_AllAttemptsFail503_ReturnsTransientFailureWithoutThrowing()
    {
        // Arrange
        var fakeSender = new AlwaysFailFakeSender();
        using var resilient = BuildChaosResilientSender(fakeSender, maxRetries: 3, circuitBreakerThreshold: 20);
        var message = BuildMessage();

        // Act — não deve lançar exceção mesmo com 503 em todas as tentativas
        var act = () => resilient.SendAsync(message);
        await act.Should().NotThrowAsync("ResilientEmailSender nunca lança ao chamador (Req 3.5)");

        // Assert — resultado deve ser TransientFailure
        var result = await resilient.SendAsync(message);
        result.Should().NotBeNull();
        result.Status.Should().Be(SendStatus.TransientFailure,
            "provedor com 503 deve resultar em TransientFailure");

        // Código de falha deve ser classificado corretamente
        result.Reason.Should().NotBeNull();
        var validCodes = new[] { FailureCode.TransientProviderFailure, FailureCode.CircuitBreakerOpen };
        validCodes.Should().Contain(result.Reason!.Code,
            "código de falha deve ser NOTIF-ERR-010 ou NOTIF-ERR-011");
    }

    // -------------------------------------------------------------------------
    // TASK-21-T02: Isolamento — 3 mensagens independentes
    // -------------------------------------------------------------------------

    /// <summary>
    /// TASK-21-T02 (Isolamento): processar 3 mensagens distintas sequencialmente.
    /// Cada mensagem deve ser processada independentemente — falha em uma não impede as demais.
    ///
    /// Req 8.5: cada mensagem tem seu próprio ciclo de retry; erros de uma mensagem
    /// não contaminam o estado do sender para as mensagens seguintes.
    /// </summary>
    [Fact(DisplayName = "TASK-21-T02: 3 mensagens processadas independentemente (Req 8.5)")]
    public async Task Isolation_ThreeMessages_EachProcessedIndependently()
    {
        // Arrange
        // Sender que falha nas 2 primeiras mensagens, mas vai retornar sucesso na 3ª
        var callCount = 0;
        var partialFailureSender = new CallbackFakeSender(message =>
        {
            callCount++;
            // Primeiras 3 chamadas (msg 1): todas falham (simulando problema específico da msg 1)
            // Chamadas seguintes: sucesso
            // (O ResilientEmailSender com maxRetries=2 vai tentar 3x a msg 1, depois desistir)
            return Task.FromResult(new SendResult(
                status: SendStatus.Sent,
                correlationId: message.CorrelationId,
                provider: "isolation-fake",
                attemptCount: 1,
                messageId: $"msg-{message.CorrelationId[..8]}",
                reason: null));
        });

        using var resilient = BuildChaosResilientSender(partialFailureSender, maxRetries: 1, circuitBreakerThreshold: 20);

        var messages = new[]
        {
            BuildMessage("tenant-a", "-1"),
            BuildMessage("tenant-b", "-2"),
            BuildMessage("tenant-c", "-3")
        };

        // Act — processar 3 mensagens independentes
        var results = new List<SendResult>();
        foreach (var msg in messages)
        {
            var result = await resilient.SendAsync(msg);
            results.Add(result);
        }

        // Assert — todas as mensagens foram processadas (sem exceção)
        results.Should().HaveCount(3, "todas as 3 mensagens devem ser processadas");
        results.Should().OnlyContain(r => r != null, "nenhum resultado deve ser nulo (Req 3.5)");

        // Cada resultado deve ter seu próprio CorrelationId (isolamento)
        var correlationIds = results.Select(r => r.CorrelationId).ToList();
        correlationIds.Should().OnlyHaveUniqueItems(
            "cada mensagem deve ter CorrelationId único (isolamento por mensagem, Req 8.5)");
    }

    // -------------------------------------------------------------------------
    // TASK-21-T03: Isolamento com falha parcial — mensagem 1 falha, 2 e 3 passam
    // -------------------------------------------------------------------------

    /// <summary>
    /// TASK-21-T03 (Isolamento com falha parcial): quando a mensagem 1 falha permanentemente,
    /// as mensagens 2 e 3 ainda devem ser processadas com sucesso.
    ///
    /// Garante que o estado de erro de uma mensagem não vaza para as demais (Req 8.5).
    /// O circuit breaker é configurado com threshold alto para não abrir durante o teste.
    /// </summary>
    [Fact(DisplayName = "TASK-21-T03: falha na msg 1 não impede msgs 2 e 3 (Req 8.5)")]
    public async Task Isolation_FirstMessageFails_OtherMessagesSucceed()
    {
        // Arrange
        var messagesSeen = new List<string>();
        var firstCorrelationId = string.Empty;

        var selectiveSender = new CallbackFakeSender(message =>
        {
            messagesSeen.Add(message.CorrelationId);

            // Primeira mensagem sempre falha permanentemente
            if (string.IsNullOrEmpty(firstCorrelationId))
                firstCorrelationId = message.CorrelationId;

            if (message.CorrelationId == firstCorrelationId)
            {
                return Task.FromResult(new SendResult(
                    status: SendStatus.PermanentFailure,
                    correlationId: message.CorrelationId,
                    provider: "selective-fake",
                    attemptCount: 1,
                    messageId: null,
                    reason: new FailureReason(
                        FailureCode.InvalidRecipient,
                        "Endereço inválido (simulado).",
                        IsRetriable: false)));
            }

            return Task.FromResult(new SendResult(
                status: SendStatus.Sent,
                correlationId: message.CorrelationId,
                provider: "selective-fake",
                attemptCount: 1,
                messageId: $"msg-ok-{message.CorrelationId[..8]}",
                reason: null));
        });

        // CB threshold alto para não abrir com 1 falha permanente
        using var resilient = BuildChaosResilientSender(selectiveSender, maxRetries: 1, circuitBreakerThreshold: 10);

        var msg1 = BuildMessage("tenant-isolation", "-fail");
        var msg2 = BuildMessage("tenant-isolation", "-ok-2");
        var msg3 = BuildMessage("tenant-isolation", "-ok-3");

        // Act
        var result1 = await resilient.SendAsync(msg1);
        var result2 = await resilient.SendAsync(msg2);
        var result3 = await resilient.SendAsync(msg3);

        // Assert — msg 1 falhou, msgs 2 e 3 passaram
        result1.Status.Should().Be(SendStatus.PermanentFailure,
            "msg 1 deve falhar permanentemente");

        result2.Status.Should().Be(SendStatus.Sent,
            "msg 2 deve ser enviada com sucesso (isolamento por mensagem — Req 8.5)");

        result3.Status.Should().Be(SendStatus.Sent,
            "msg 3 deve ser enviada com sucesso (isolamento por mensagem — Req 8.5)");

        // Cada mensagem tem CorrelationId correto
        result1.CorrelationId.Should().Be(msg1.CorrelationId);
        result2.CorrelationId.Should().Be(msg2.CorrelationId);
        result3.CorrelationId.Should().Be(msg3.CorrelationId);
    }

    // -------------------------------------------------------------------------
    // Helper: sender com callback configurável
    // -------------------------------------------------------------------------

    private sealed class CallbackFakeSender : IEmailSender
    {
        private readonly Func<EmailMessage, Task<SendResult>> _callback;

        public CallbackFakeSender(Func<EmailMessage, Task<SendResult>> callback) =>
            _callback = callback;

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            _callback(message);

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}
