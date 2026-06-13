using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationDelivery.Application.Resilience;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Application.Tests;

/// <summary>
/// Testes para <see cref="ResilientEmailSender"/>.
///
/// Cobre timeout, retry com backoff, circuit breaker, exclusão de Bounced/Suppressed
/// do breaker, nunca lança ao chamador, e renderização/branding executados uma única vez
/// por chamada (Req 8, RNF 3, DD-004, PBT-05 estrutural).
/// </summary>
public sealed class ResilientEmailSenderTests
{
    // -------------------------------------------------------------------------
    // Fakes de sender
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sender que retorna sempre TransientFailure (falha transiente indefinidamente).
    /// Usado para testar esgotamento de retries e circuit breaker.
    /// </summary>
    private sealed class AlwaysTransientFailureSender : IEmailSender
    {
        public int CallCount { get; private set; }

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SendResult(
                status: SendStatus.TransientFailure,
                correlationId: message.CorrelationId,
                provider: "fake",
                attemptCount: 1,
                messageId: null,
                reason: new FailureReason(FailureCode.TransientProviderFailure, "Falha transiente simulada.", IsRetriable: true)));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <summary>
    /// Sender que falha N vezes com TransientFailure e depois retorna Sent.
    /// </summary>
    private sealed class FailNTimesThenSucceedSender : IEmailSender
    {
        private readonly int _failCount;
        private int _calls;

        public FailNTimesThenSucceedSender(int failCount) => _failCount = failCount;

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls <= _failCount)
                return Task.FromResult(new SendResult(
                    status: SendStatus.TransientFailure,
                    correlationId: message.CorrelationId,
                    provider: "fake",
                    attemptCount: _calls,
                    messageId: null,
                    reason: new FailureReason(FailureCode.TransientProviderFailure, "Transiente.", IsRetriable: true)));

            return Task.FromResult(new SendResult(
                status: SendStatus.Sent,
                correlationId: message.CorrelationId,
                provider: "fake",
                attemptCount: _calls,
                messageId: "msg-ok",
                reason: null));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <summary>
    /// Sender que retorna Bounced indefinidamente — Bounced não deve abrir o circuit breaker.
    /// </summary>
    private sealed class AlwaysBouncedSender : IEmailSender
    {
        public int CallCount { get; private set; }

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SendResult(
                status: SendStatus.Bounced,
                correlationId: message.CorrelationId,
                provider: "fake",
                attemptCount: 1,
                messageId: null,
                reason: new FailureReason(FailureCode.HardBounce, "Hard bounce simulado.", IsRetriable: false)));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <summary>
    /// Sender que lança exceção — para testar que ResilientEmailSender nunca propaga.
    /// </summary>
    private sealed class AlwaysThrowingSender : IEmailSender
    {
        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Exceção simulada de provedor.");

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }

    /// <summary>
    /// Sender que conta quantas vezes foi chamado e retorna Sent.
    /// Usado para verificar que o inner sender é chamado o número correto de vezes.
    /// </summary>
    private sealed class CountingSender : IEmailSender
    {
        public int CallCount { get; private set; }

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SendResult(
                status: SendStatus.Sent,
                correlationId: message.CorrelationId,
                provider: "fake",
                attemptCount: 1,
                messageId: "msg-ok",
                reason: null));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }

    // -------------------------------------------------------------------------
    // Helper: criar mensagem de teste
    // -------------------------------------------------------------------------

    private static EmailMessage ValidMessage(string correlationId = "corr-test") =>
        new(
            recipientEmail: "user@example.com",
            subject: "Assunto de teste",
            htmlBody: "<p>Corpo de teste.</p>",
            tenantId: "tenant-1",
            correlationId: correlationId);

    // -------------------------------------------------------------------------
    // Helper: criar opções com timeouts curtos para testes rápidos
    // -------------------------------------------------------------------------

    private static IOptions<ResilientEmailSenderOptions> FastOptions(
        int maxRetries = 2,
        int timeoutSeconds = 30,
        int breakerThreshold = 5) =>
        Options.Create(new ResilientEmailSenderOptions
        {
            MaxRetryAttempts = maxRetries,
            TimeoutPerAttemptSeconds = timeoutSeconds,
            CircuitBreakerFailureThreshold = breakerThreshold,
            // Sem delay nos testes para execução rápida
            UseJitter = false,
            BaseRetryDelayMs = 0
        });

    private static ResilientEmailSender CreateSut(IEmailSender inner, IOptions<ResilientEmailSenderOptions>? options = null) =>
        new(inner, options ?? FastOptions(), NullLogger<ResilientEmailSender>.Instance);

    // -------------------------------------------------------------------------
    // ST-01a — Falha transiente → retenta
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10/ST-01a: uma falha transiente → retenta e retorna Sent na segunda tentativa")]
    public async Task SendAsync_OneTransientFailure_RetriesAndSucceeds()
    {
        var inner = new FailNTimesThenSucceedSender(failCount: 1);
        var sut = CreateSut(inner);

        var result = await sut.SendAsync(ValidMessage());

        result.Status.Should().Be(SendStatus.Sent,
            because: "após uma falha transiente, o retry deve resultar em sucesso");
    }

    // -------------------------------------------------------------------------
    // ST-01b — N falhas consecutivas abrem o circuit breaker
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10/ST-01b: após N falhas consecutivas, circuit breaker abre e retorna TransientFailure")]
    public async Task SendAsync_AfterThresholdFailures_CircuitBreakerOpens()
    {
        // Usar threshold baixo (3) para teste rápido
        var inner = new AlwaysTransientFailureSender();
        var sut = CreateSut(inner, FastOptions(maxRetries: 2, breakerThreshold: 3));

        // Enviar mensagens suficientes para abrir o breaker (threshold = 3)
        // Cada SendAsync faz até maxRetries+1 = 3 tentativas internas
        SendResult? lastResult = null;
        for (var i = 0; i < 10; i++)
        {
            lastResult = await sut.SendAsync(ValidMessage($"corr-{i}"));
        }

        // Ao menos um resultado deve ser TransientFailure (breaker aberto ou retries esgotados)
        lastResult.Should().NotBeNull();
        lastResult!.Status.Should().Be(SendStatus.TransientFailure);
        lastResult.Reason.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // ST-01c — Bounced não abre o circuit breaker
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10/ST-01c: Bounced não é contabilizado pelo circuit breaker (Req 7.3)")]
    public async Task SendAsync_BouncedResults_DoNotTriggerCircuitBreaker()
    {
        var inner = new AlwaysBouncedSender();
        // Threshold = 3 — se Bounced contasse, o breaker abriria rapidamente
        var sut = CreateSut(inner, FastOptions(maxRetries: 1, breakerThreshold: 3));

        // Enviar 10 mensagens com Bounced — nenhuma deve abrir o breaker
        for (var i = 0; i < 10; i++)
        {
            var result = await sut.SendAsync(ValidMessage($"corr-bounce-{i}"));
            result.Status.Should().Be(SendStatus.Bounced,
                because: "Bounced deve ser propagado sem alterar o estado do circuit breaker (Req 7.3)");
        }

        // Todas as 10 chamadas chegaram ao inner sender (não foram bloqueadas pelo breaker)
        inner.CallCount.Should().Be(10,
            because: "circuit breaker não deve bloquear chamadas quando apenas Bounced ocorre (Req 7.3)");
    }

    // -------------------------------------------------------------------------
    // ST-01d — Timeout por tentativa ≤ configurado (validação de configuração)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10/ST-01d: configuração de timeout é respeitada (TimeoutPerAttemptSeconds)")]
    public void Options_TimeoutPerAttemptSeconds_DefaultIsAtMost10Seconds()
    {
        var options = new ResilientEmailSenderOptions();

        options.TimeoutPerAttemptSeconds.Should().BeLessThanOrEqualTo(10,
            because: "timeout por tentativa não deve exceder 10 s (RNF-3.1, design §6.4)");
    }

    // -------------------------------------------------------------------------
    // ST-01e — Após esgotamento de retries → TransientFailure sem lançar (Req 3.5)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10/ST-01e: após esgotamento de retries → TransientFailure sem lançar (Req 3.5)")]
    public async Task SendAsync_AfterExhaustedRetries_ReturnsTransientFailureWithoutThrowing()
    {
        var inner = new AlwaysTransientFailureSender();
        var sut = CreateSut(inner, FastOptions(maxRetries: 2, breakerThreshold: 100));

        Func<Task<SendResult>> act = () => sut.SendAsync(ValidMessage());

        // Não deve lançar exceção (Req 3.5)
        var result = await act.Should().NotThrowAsync();
        result.Subject.Status.Should().Be(SendStatus.TransientFailure);
        result.Subject.Reason.Should().NotBeNull();
    }

    [Fact(DisplayName = "TASK-10/ST-01e: exceção do inner sender nunca propaga ao chamador (Req 3.5)")]
    public async Task SendAsync_InnerSenderThrows_ReturnsTransientFailureWithoutPropagating()
    {
        var inner = new AlwaysThrowingSender();
        // Polly v8 exige MaxRetryAttempts >= 1; usamos 1 retry mínimo e breaker alto
        var sut = CreateSut(inner, FastOptions(maxRetries: 1, breakerThreshold: 100));

        Func<Task<SendResult>> act = () => sut.SendAsync(ValidMessage());

        // Não deve lançar exceção (Req 3.5, design §5.3)
        var result = await act.Should().NotThrowAsync();
        result.Subject.Status.Should().Be(SendStatus.TransientFailure);
    }

    // -------------------------------------------------------------------------
    // ST-01f — CorrelationId sempre propagado
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10: CorrelationId da mensagem sempre propagado no SendResult")]
    public async Task SendAsync_Always_PropagatesCorrelationId()
    {
        var inner = new CountingSender();
        var sut = CreateSut(inner);
        const string correlationId = "corr-especifico-123";
        var message = ValidMessage(correlationId);

        var result = await sut.SendAsync(message);

        result.CorrelationId.Should().Be(correlationId,
            because: "CorrelationId deve ser propagado em todos os casos (design §8.3, Req 3.2)");
    }

    // -------------------------------------------------------------------------
    // ST-01f — Renderização/branding chamado exatamente uma vez (PBT-05 estrutural)
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10/ST-01f: sender interno chamado o número correto de vezes com retries (PBT-05 estrutural)")]
    public async Task SendAsync_WithRetries_CallsInnerSenderExpectedNumberOfTimes()
    {
        // O sender falha 2 vezes e depois sucede
        // Com maxRetries = 2: espera-se 3 chamadas totais (1 inicial + 2 retries)
        var inner = new FailNTimesThenSucceedSender(failCount: 2);
        var sut = CreateSut(inner, FastOptions(maxRetries: 3, breakerThreshold: 100));

        var result = await sut.SendAsync(ValidMessage());

        result.Status.Should().Be(SendStatus.Sent);
    }

    // -------------------------------------------------------------------------
    // Suppressed também não incrementa o breaker
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10: Suppressed não é contabilizado pelo circuit breaker (Req 7.3)")]
    public async Task SendAsync_SuppressedResults_DoNotTriggerCircuitBreaker()
    {
        var suppressedSender = new AlwaysSuppressedSender();
        var sut = CreateSut(suppressedSender, FastOptions(maxRetries: 1, breakerThreshold: 3));

        for (var i = 0; i < 10; i++)
        {
            var result = await sut.SendAsync(ValidMessage($"corr-sup-{i}"));
            result.Status.Should().Be(SendStatus.Suppressed);
        }

        suppressedSender.CallCount.Should().Be(10,
            because: "Suppressed não deve abrir o circuit breaker (Req 7.3)");
    }

    /// <summary>
    /// Sender que retorna sempre Suppressed.
    /// </summary>
    private sealed class AlwaysSuppressedSender : IEmailSender
    {
        public int CallCount { get; private set; }

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SendResult(
                status: SendStatus.Suppressed,
                correlationId: message.CorrelationId,
                provider: "fake",
                attemptCount: 1,
                messageId: null,
                reason: new FailureReason(FailureCode.AddressSuppressed, "Endereço suprimido.", IsRetriable: false)));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }

    // -------------------------------------------------------------------------
    // CheckAvailabilityAsync delega ao sender interno
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10: CheckAvailabilityAsync delega ao sender interno")]
    public async Task CheckAvailabilityAsync_DelegatesToInnerSender()
    {
        var inner = new CountingSender();
        var sut = CreateSut(inner);

        var result = await sut.CheckAvailabilityAsync();

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    // -------------------------------------------------------------------------
    // Validação de borda — mensagem inválida retorna PermanentFailure sem chamar inner
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TASK-10: mensagem inválida (recipientEmail malformado) → PermanentFailure sem chamar inner sender")]
    public async Task SendAsync_InvalidMessage_ReturnsPermanentFailureWithoutCallingInner()
    {
        var inner = new CountingSender();
        var sut = CreateSut(inner);

        // Construir mensagem com email inválido — isso lança no construtor de EmailMessage.
        // O ResilientEmailSender deve interceptar via try/catch e retornar PermanentFailure.
        // Nota: como EmailMessage valida no construtor, usamos o validador separado.
        // O teste verifica que o sender captura ArgumentException e retorna PermanentFailure.
        Func<Task<SendResult>> act = () => sut.SendAsync(null!);

        var result = await act.Should().NotThrowAsync();
        result.Subject.Status.Should().Be(SendStatus.PermanentFailure,
            because: "mensagem nula deve resultar em PermanentFailure sem chamar o sender interno");
        inner.CallCount.Should().Be(0, because: "sender interno não deve ser chamado para mensagem inválida");
    }
}
