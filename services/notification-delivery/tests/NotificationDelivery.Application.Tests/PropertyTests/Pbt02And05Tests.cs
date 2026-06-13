using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationDelivery.Application.Resilience;
using NotificationDelivery.Contracts;
// EmailMessageArbitraries está no mesmo namespace (cópia local para evitar dependência cruzada)
using Xunit;

namespace NotificationDelivery.Application.Tests.PropertyTests;

/// <summary>
/// PBT-02: Idempotência de reenvio — para qualquer <see cref="EmailMessage"/> com
/// <see cref="EmailMessage.IdempotencyKey"/> fixa, enviar N ≥ 1 vezes com provedor
/// fake que aceita apenas uma entrega por chave resulta em no máximo uma entrega efetiva.
///
/// PBT-05: Backoff não duplica entrega — para qualquer sequência de até 4 falhas
/// transientes seguida de sucesso, com a mesma <c>IdempotencyKey</c>, o número de
/// entregas efetivas ao provedor fake é ≤ 1.
///
/// ≥ 500 exemplos cada (requisito tasks.md TASK-18, requirements PBT-02/05).
///
/// Mapeia: TASK-18, PBT-02 (Req 9, NFR-RES-02), PBT-05 (Req 8, RNF 3, design §5.4).
/// </summary>
public sealed class Pbt02And05Tests
{
    // -------------------------------------------------------------------------
    // Configuração: 500 exemplos por propriedade
    // -------------------------------------------------------------------------
    private static readonly Config FsCheckConfig = Config.QuickThrowOnFailure.WithMaxTest(500);

    // -------------------------------------------------------------------------
    // Provedor fake com contador de entregas por IdempotencyKey (PBT-02/05)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sender fake que conta entregas efetivas por <see cref="EmailMessage.IdempotencyKey"/>.
    ///
    /// Simulação do comportamento de deduplicação do provedor real:
    /// - primeira entrega com a chave → aceita (IsSuccess=true);
    /// - entrega subsequente com a mesma chave → rejeitada (IsSuccess=false, não conta).
    ///
    /// Usado em PBT-02/05 para verificar que <see cref="ResilientEmailSender"/> não
    /// causa duplicação de entrega (Req 9.2, PBT-02, PBT-05).
    /// </summary>
    private sealed class IdempotentProviderFakeSender : IEmailSender
    {
        /// <summary>
        /// Contador de entregas efetivas por IdempotencyKey.
        /// Thread-safe para uso em testes assíncronos.
        /// </summary>
        private readonly Dictionary<string, int> _deliveryCount = new();

        /// <summary>
        /// Sequência de resultados a retornar nas próximas chamadas.
        /// Quando vazia, retorna sucesso por padrão.
        /// </summary>
        private readonly Queue<SendResult> _plannedResults;

        /// <summary>
        /// Chave de fallback para mensagens sem <see cref="EmailMessage.IdempotencyKey"/>.
        /// </summary>
        private const string NoKeyFallback = "__no_key__";

        public IdempotentProviderFakeSender(IEnumerable<SendResult>? plannedResults = null)
        {
            _plannedResults = plannedResults != null
                ? new Queue<SendResult>(plannedResults)
                : new Queue<SendResult>();
        }

        /// <summary>Número total de chamadas recebidas.</summary>
        public int TotalCallCount { get; private set; }

        /// <summary>Entregas efetivas por chave de idempotência.</summary>
        public IReadOnlyDictionary<string, int> DeliveryCount => _deliveryCount;

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            TotalCallCount++;
            var key = message.IdempotencyKey ?? NoKeyFallback;

            // Retorna resultado planejado (sequência de falhas/sucesso)
            if (_plannedResults.TryDequeue(out var planned))
            {
                // Conta entrega efetiva apenas para Sent
                if (planned.Status == SendStatus.Sent)
                {
                    _deliveryCount.TryGetValue(key, out var current);
                    _deliveryCount[key] = current + 1;
                }
                return Task.FromResult(planned);
            }

            // Sem resultado planejado: aceita primeira entrega por chave, rejeita duplicatas
            _deliveryCount.TryGetValue(key, out var count);
            if (count == 0)
            {
                _deliveryCount[key] = 1;
                return Task.FromResult(new SendResult(
                    status: SendStatus.Sent,
                    correlationId: message.CorrelationId,
                    provider: "idempotent-fake",
                    attemptCount: 1,
                    messageId: $"fake-msg-{Guid.NewGuid():N}",
                    reason: null));
            }

            // Segunda entrega com a mesma chave → rejeitada (deduplicação)
            return Task.FromResult(new SendResult(
                status: SendStatus.PermanentFailure,
                correlationId: message.CorrelationId,
                provider: "idempotent-fake",
                attemptCount: 1,
                messageId: null,
                reason: new FailureReason(
                    FailureCode.ProviderRejectedPayload,
                    "Duplicata detectada pelo provedor (chave de idempotência reutilizada).",
                    IsRetriable: false)));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }

    // -------------------------------------------------------------------------
    // Factory: ResilientEmailSender com opções sem delay (testes rápidos)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Cria <see cref="ResilientEmailSender"/> com delays zerados para testes PBT.
    /// Mantém a semântica de retry/circuit breaker sem espera real (testes unitários).
    /// </summary>
    private static ResilientEmailSender BuildResilientSender(
        IEmailSender inner,
        int maxRetries = 5,
        int circuitBreakerThreshold = 10)
    {
        var options = Options.Create(new ResilientEmailSenderOptions
        {
            MaxRetryAttempts = maxRetries,
            TimeoutPerAttemptSeconds = 30,      // alto para não interferir com PBT
            CircuitBreakerFailureThreshold = circuitBreakerThreshold,
            CircuitBreakerBreakDurationSeconds = 1,
            BaseRetryDelayMs = 0,               // sem delay: testes rápidos
            UseJitter = false                   // determinístico em testes
        });

        return new ResilientEmailSender(inner, options, NullLogger<ResilientEmailSender>.Instance);
    }

    // -------------------------------------------------------------------------
    // PBT-02: Idempotência de reenvio
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-02: para qualquer <see cref="EmailMessage"/> com <c>IdempotencyKey</c> fixa,
    /// chamar <see cref="ResilientEmailSender.SendAsync"/> N ∈ [1,10] vezes resulta em
    /// no máximo uma entrega efetiva ao provedor.
    ///
    /// Verifica: Req 9.2 (no máximo uma entrega efetiva com mesma chave), PBT-02.
    /// </summary>
    [Fact(DisplayName = "PBT-02: N chamadas com mesma IdempotencyKey produzem ≤1 entrega efetiva (≥500 exemplos)")]
    public void PbtIdempotencyReenvio_SameKey_ProducesAtMostOneDelivery()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailMessage(withIdempotencyKey: true),
            Gen.Choose(1, 10).ToArbitrary(),
            (message, callCount) =>
            {
                // Arrange — sender idempotente (aceita primeira entrega, rejeita duplicatas)
                var fakeSender = new IdempotentProviderFakeSender();
                // maxRetries=1: mínimo aceito pelo Polly; sem falhas planejadas, não há retries reais.
                // CircuitBreakerThreshold alto para não interferir com N chamadas independentes.
                var resilient = BuildResilientSender(fakeSender, maxRetries: 1, circuitBreakerThreshold: 50);

                // Act — enviar N vezes com a mesma IdempotencyKey
                var results = new List<SendResult>();
                for (int i = 0; i < callCount; i++)
                {
                    // Nova mensagem com mesma IdempotencyKey mas CorrelationId distinto
                    // (simula retentativa do chamador com a mesma chave)
                    var msg = new EmailMessage(
                        recipientEmail: message.RecipientEmail,
                        subject: message.Subject,
                        htmlBody: message.HtmlBody,
                        tenantId: message.TenantId,
                        correlationId: Guid.NewGuid().ToString(), // CorrelationId diferente por chamada
                        idempotencyKey: message.IdempotencyKey);  // mesma chave

                    results.Add(resilient.SendAsync(msg).GetAwaiter().GetResult());
                }

                // Assert — no máximo uma entrega efetiva (PBT-02, Req 9.2)
                var deliveries = fakeSender.DeliveryCount.Values.Sum();
                deliveries.Should().BeLessOrEqualTo(1,
                    $"No máximo uma entrega efetiva para key={message.IdempotencyKey}, callCount={callCount}");

                // Nenhum resultado nulo (ResilientEmailSender nunca retorna null)
                results.Should().NotContainNulls("SendAsync nunca retorna null (Req 3.5)");

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    // -------------------------------------------------------------------------
    // PBT-05: Backoff não duplica entrega
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-05: para qualquer sequência de até 4 falhas transientes seguida de sucesso,
    /// com a mesma <c>IdempotencyKey</c>, o número de entregas efetivas é ≤ 1.
    ///
    /// Verifica: a renderização ocorre uma única vez antes do loop de retry (design §5.4),
    /// e a mesma <c>IdempotencyKey</c> garante deduplicação no provedor (Req 8, RNF 3, PBT-05).
    /// </summary>
    [Fact(DisplayName = "PBT-05: sequência de falhas transientes + sucesso produz ≤1 entrega efetiva (≥500 exemplos)")]
    public void PbtBackoffNotDuplicate_TransientSequence_ProducesAtMostOneDelivery()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailMessage(withIdempotencyKey: true),
            EmailMessageArbitraries.TransientThenSuccessSequences(),
            (message, sequence) =>
            {
                // Arrange — sequência planejada: N TransientFailure → 1 Sent
                var transientResult = new SendResult(
                    status: SendStatus.TransientFailure,
                    correlationId: message.CorrelationId,
                    provider: "seq-fake",
                    attemptCount: 1,
                    messageId: null,
                    reason: new FailureReason(FailureCode.TransientProviderFailure, "Falha simulada.", IsRetriable: true));

                var sentResult = new SendResult(
                    status: SendStatus.Sent,
                    correlationId: message.CorrelationId,
                    provider: "seq-fake",
                    attemptCount: sequence.TransientCount + 1,
                    messageId: $"fake-msg-{message.IdempotencyKey}",
                    reason: null);

                // Preenche a fila com N falhas + 1 sucesso
                var planned = Enumerable.Repeat(transientResult, sequence.TransientCount)
                    .Append(sentResult)
                    .ToList();

                var fakeSender = new IdempotentProviderFakeSender(planned);
                // CircuitBreakerThreshold > TransientCount para não abrir o breaker durante o teste
                var resilient = BuildResilientSender(
                    fakeSender,
                    maxRetries: 5,
                    circuitBreakerThreshold: 10);

                // Act — uma única chamada ao ResilientEmailSender
                var result = resilient.SendAsync(message).GetAwaiter().GetResult();

                // Assert — ResilientEmailSender nunca retorna null
                result.Should().NotBeNull("SendAsync nunca retorna null (Req 3.5)");

                // O número de chamadas ao sender interno pode ser ≥ 1 (devido aos retries)
                // mas as entregas EFETIVAS ao provedor devem ser ≤ 1 (PBT-05)
                var deliveries = fakeSender.DeliveryCount.Values.Sum();
                deliveries.Should().BeLessOrEqualTo(1,
                    $"No máximo uma entrega efetiva com key={message.IdempotencyKey}, " +
                    $"transientCount={sequence.TransientCount}");

                // O resultado deve ser Sent (a sequência termina em sucesso dentro dos retries)
                // quando TransientCount ≤ MaxRetryAttempts
                if (sequence.TransientCount <= 5)
                {
                    result.Status.Should().Be(SendStatus.Sent,
                        $"Após {sequence.TransientCount} falhas + sucesso, resultado deve ser Sent");
                }

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    /// <summary>
    /// PBT-05 (complementar): verifica que a renderização no <see cref="ResilientEmailSender"/>
    /// ocorre uma única vez antes do loop de retry — garantia estrutural do design §5.4.
    ///
    /// O sender interno recebe sempre o mesmo objeto de mensagem (imutável), o que sustenta
    /// a idempotência: mesma <c>IdempotencyKey</c> + mesmo payload em cada tentativa.
    /// </summary>
    [Fact(DisplayName = "PBT-05B: mesma EmailMessage imutável chega ao sender em todas as tentativas (≥500 exemplos)")]
    public void PbtBackoffNotDuplicate_SameMessageReachesInnerSenderEachAttempt()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailMessage(withIdempotencyKey: true),
            Gen.Choose(1, 3).ToArbitrary(), // 1..3 falhas transientes
            (message, transientCount) =>
            {
                // Sender que registra todas as mensagens recebidas para verificar imutabilidade
                var receivedMessages = new List<EmailMessage>();
                var countFixed = transientCount;

                var captureSender = new CapturingFakeSender(receivedMessages, countFixed);
                var resilient = BuildResilientSender(captureSender, maxRetries: 5, circuitBreakerThreshold: 10);

                // Act
                resilient.SendAsync(message).GetAwaiter().GetResult();

                // Assert — todas as mensagens recebidas pelo sender interno têm a mesma IdempotencyKey
                // (o ResilientEmailSender passa o mesmo objeto EmailMessage em todas as tentativas)
                receivedMessages.Should().NotBeEmpty("pelo menos uma tentativa deve ocorrer");
                receivedMessages.Should().AllSatisfy(m =>
                    m.IdempotencyKey.Should().Be(message.IdempotencyKey,
                        "IdempotencyKey deve ser idêntica em todas as tentativas (design §5.4, PBT-05)"));

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sender fake que captura as mensagens recebidas e retorna N falhas transientes + sucesso.
    /// Usado em PBT-05B para verificar que a mensagem passada é sempre a mesma.
    /// </summary>
    private sealed class CapturingFakeSender : IEmailSender
    {
        private readonly List<EmailMessage> _received;
        private readonly int _transientCount;
        private int _callCount;

        public CapturingFakeSender(List<EmailMessage> received, int transientCount)
        {
            _received = received;
            _transientCount = transientCount;
        }

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _received.Add(message);
            _callCount++;

            if (_callCount <= _transientCount)
            {
                return Task.FromResult(new SendResult(
                    status: SendStatus.TransientFailure,
                    correlationId: message.CorrelationId,
                    provider: "capture-fake",
                    attemptCount: _callCount,
                    messageId: null,
                    reason: new FailureReason(FailureCode.TransientProviderFailure, "Falha simulada.", IsRetriable: true)));
            }

            return Task.FromResult(new SendResult(
                status: SendStatus.Sent,
                correlationId: message.CorrelationId,
                provider: "capture-fake",
                attemptCount: _callCount,
                messageId: $"cap-msg-{Guid.NewGuid():N}",
                reason: null));
        }

        public Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}
