using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationDelivery.Application.Resilience;
using NotificationDelivery.Contracts;
using NotificationDelivery.Infrastructure.Telemetry;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using Xunit;

namespace NotificationDelivery.Infrastructure.Tests.PropertyTests;

/// <summary>
/// PBT-03: Anti-vazamento de PII em telemetria.
///
/// Para qualquer <see cref="EmailMessage"/> válido, após processar o envio pelo
/// <see cref="ResilientEmailSender"/> com <see cref="EmailPiiScrubberEnricher"/> ativo,
/// nenhum log capturado contém e-mail em claro (padrão RFC 5321 simplificado).
///
/// Verifica também que:
/// - mensagens de falha (<c>FailureReason.Message</c>) não contêm e-mail em claro;
/// - campos de struct interna (CorrelationId etc.) não expõem o e-mail do destinatário.
///
/// ≥ 500 exemplos (requisito tasks.md TASK-19, requirements PBT-03).
///
/// Mapeia: TASK-19, PBT-03, RNF 4 (nenhum PII em log), DD-008.
/// </summary>
public sealed class Pbt03Tests
{
    // -------------------------------------------------------------------------
    // Regex para detectar e-mail em claro nos logs (RFC 5321 simplificado)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Padrão que detecta endereço de e-mail em claro nos logs.
    /// Se este padrão casar em qualquer log, significa vazamento de PII (PBT-03).
    /// </summary>
    private static readonly Regex EmailPattern = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // -------------------------------------------------------------------------
    // Configuração FsCheck: 500 exemplos
    // -------------------------------------------------------------------------
    private static readonly Config FsCheckConfig = Config.QuickThrowOnFailure.WithMaxTest(500);

    // -------------------------------------------------------------------------
    // Fake sender que loga o RecipientEmail para forçar o enriquecedor a agir
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sender fake que escreve o RecipientEmail como propriedade de log estruturado.
    /// Simula o comportamento de um sender real que logaria dados da mensagem.
    /// O <see cref="EmailPiiScrubberEnricher"/> deve interceptar e mascarar esses valores.
    /// </summary>
    private sealed class LoggingFakeSender : IEmailSender
    {
        private readonly ILogger<LoggingFakeSender> _logger;
        private readonly SendResult _result;

        public LoggingFakeSender(ILogger<LoggingFakeSender> logger, SendResult result)
        {
            _logger = logger;
            _result = result;
        }

        public Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            // Loga propriedades que o enriquecedor deve mascarar
            _logger.LogInformation(
                "Enviando mensagem. RecipientEmail={RecipientEmail} Subject={Subject} TenantId={TenantId} CorrelationId={CorrelationId}",
                message.RecipientEmail,  // PII — deve ser mascarado pelo enriquecedor
                message.Subject,
                message.TenantId,
                message.CorrelationId);

            // Loga também a propriedade "Email" (outro nome coberto pelo enriquecedor)
            _logger.LogDebug(
                "Detalhes do destinatário. Email={Email} Recipient={recipient}",
                message.RecipientEmail,  // PII — deve ser mascarado
                message.RecipientEmail); // PII — deve ser mascarado

            return Task.FromResult(_result);
        }

        public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckAvailabilityAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
    }

    // -------------------------------------------------------------------------
    // Helper: construir logger Serilog com InMemorySink + EmailPiiScrubberEnricher
    // -------------------------------------------------------------------------

    /// <summary>
    /// Cria um <see cref="InMemorySink"/> e configura Serilog com
    /// <see cref="EmailPiiScrubberEnricher"/> ativo para captura de logs.
    /// </summary>
    private static (InMemorySink sink, Serilog.ILogger logger) BuildLoggerWithSink()
    {
        var sink = new InMemorySink();

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.With<EmailPiiScrubberEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        return (sink, serilogLogger);
    }

    /// <summary>
    /// Adapta <see cref="Serilog.ILogger"/> para <see cref="ILogger{T}"/> usando
    /// um wrapper simples baseado em Serilog.Extensions.Logging (sem dep. extra —
    /// usa o canal ILoggerFactory do próprio Serilog via LoggerSinkConfiguration).
    ///
    /// Neste contexto de teste, criamos um <see cref="ILogger{T}"/> que grava
    /// diretamente no Serilog estático configurado como <c>Log.Logger</c>.
    /// </summary>
    private static ILogger<T> BuildMicrosoftLogger<T>(Serilog.ILogger serilogLogger)
    {
        // Usa o Serilog.ILogger como backing para um ILogger<T> via adaptador manual
        return new SerilogMicrosoftLoggerAdapter<T>(serilogLogger);
    }

    // -------------------------------------------------------------------------
    // Adaptador minimal ILogger<T> → Serilog.ILogger
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adaptador leve de <see cref="ILogger{T}"/> que escreve no <see cref="Serilog.ILogger"/> fornecido.
    /// Evita dependência de <c>Serilog.Extensions.Logging</c> nos tests.
    /// </summary>
    private sealed class SerilogMicrosoftLoggerAdapter<T> : ILogger<T>
    {
        private readonly Serilog.ILogger _inner;

        public SerilogMicrosoftLoggerAdapter(Serilog.ILogger inner)
        {
            _inner = inner.ForContext<T>();
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var serilogLevel = logLevel switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };

            if (!_inner.IsEnabled(serilogLevel))
                return;

            // Tenta extrair propriedades estruturadas do state (MessageTemplate + values)
            // IReadOnlyList<KeyValuePair<string, object?>> é o formato padrão do MEL
            if (state is IReadOnlyList<KeyValuePair<string, object?>> props)
            {
                // Monta um log event com todas as propriedades estruturadas
                var template = props.FirstOrDefault(p => p.Key == "{OriginalFormat}").Value?.ToString()
                               ?? formatter(state, exception);

                // Escreve diretamente usando Write com template e values
                // Extrai apenas os valores (na ordem do template) para passar ao Serilog
                var values = props
                    .Where(p => p.Key != "{OriginalFormat}")
                    .Select(p => p.Value)
                    .ToArray<object?>();

                _inner.Write(serilogLevel, exception, template, values);
            }
            else
            {
                _inner.Write(serilogLevel, exception, "{Message}", formatter(state, exception));
            }
        }
    }

    // -------------------------------------------------------------------------
    // PBT-03A: logs de envio bem-sucedido não contêm e-mail em claro
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-03A: para qualquer <see cref="EmailMessage"/> válido com envio bem-sucedido,
    /// nenhum evento de log capturado pelo <see cref="InMemorySink"/> contém
    /// endereço de e-mail em claro (PBT-03, RNF 4, DD-008).
    ///
    /// ≥ 500 exemplos.
    /// </summary>
    [Fact(DisplayName = "PBT-03A: logs de envio bem-sucedido não contêm e-mail em claro (≥500 exemplos)")]
    public void PbtAntiPii_SuccessPath_NoEmailInLogs()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailMessage(withIdempotencyKey: false),
            message =>
            {
                // Arrange
                var (sink, serilogLogger) = BuildLoggerWithSink();

                var sentResult = new SendResult(
                    status: SendStatus.Sent,
                    correlationId: message.CorrelationId,
                    provider: "pbt03-fake",
                    attemptCount: 1,
                    messageId: "msg-pbt03-ok",
                    reason: null);

                var innerSenderLogger = BuildMicrosoftLogger<LoggingFakeSender>(serilogLogger);
                var innerSender = new LoggingFakeSender(innerSenderLogger, sentResult);

                var resiLogger = BuildMicrosoftLogger<ResilientEmailSender>(serilogLogger);
                var resilient = new ResilientEmailSender(
                    innerSender,
                    Options.Create(new ResilientEmailSenderOptions
                    {
                        MaxRetryAttempts = 1,
                        TimeoutPerAttemptSeconds = 30,
                        CircuitBreakerFailureThreshold = 10,
                        CircuitBreakerBreakDurationSeconds = 1,
                        BaseRetryDelayMs = 0,
                        UseJitter = false
                    }),
                    resiLogger);

                // Act
                resilient.SendAsync(message).GetAwaiter().GetResult();

                // Assert — nenhum log deve conter o e-mail em claro
                AssertNoEmailInLogs(sink, message.RecipientEmail);

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    // -------------------------------------------------------------------------
    // PBT-03B: logs de falha permanente não contêm e-mail em claro
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-03B: para qualquer <see cref="EmailMessage"/> válido com falha permanente,
    /// nenhum evento de log nem a <c>FailureReason.Message</c> contêm e-mail em claro
    /// (PBT-03, RNF 4, DD-008).
    ///
    /// ≥ 500 exemplos.
    /// </summary>
    [Fact(DisplayName = "PBT-03B: logs de falha permanente não contêm e-mail em claro (≥500 exemplos)")]
    public void PbtAntiPii_FailurePath_NoEmailInLogs()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailMessage(withIdempotencyKey: true),
            message =>
            {
                // Arrange
                var (sink, serilogLogger) = BuildLoggerWithSink();

                var failureResult = new SendResult(
                    status: SendStatus.PermanentFailure,
                    correlationId: message.CorrelationId,
                    provider: "pbt03-fail-fake",
                    attemptCount: 1,
                    messageId: null,
                    reason: new FailureReason(
                        FailureCode.ProviderRejectedPayload,
                        // Mensagem de falha NÃO deve conter e-mail (RNF 4)
                        $"Entrega rejeitada para correlationId={message.CorrelationId}.",
                        IsRetriable: false));

                var innerSenderLogger = BuildMicrosoftLogger<LoggingFakeSender>(serilogLogger);
                var innerSender = new LoggingFakeSender(innerSenderLogger, failureResult);

                var resiLogger = BuildMicrosoftLogger<ResilientEmailSender>(serilogLogger);
                var resilient = new ResilientEmailSender(
                    innerSender,
                    Options.Create(new ResilientEmailSenderOptions
                    {
                        MaxRetryAttempts = 1,
                        TimeoutPerAttemptSeconds = 30,
                        CircuitBreakerFailureThreshold = 10,
                        CircuitBreakerBreakDurationSeconds = 1,
                        BaseRetryDelayMs = 0,
                        UseJitter = false
                    }),
                    resiLogger);

                // Act
                var result = resilient.SendAsync(message).GetAwaiter().GetResult();

                // Assert — nenhum log deve conter o e-mail em claro
                AssertNoEmailInLogs(sink, message.RecipientEmail);

                // Assert — FailureReason.Message não deve conter e-mail em claro
                if (result.Reason is not null)
                {
                    var reasonMsg = result.Reason.Message;
                    if (EmailPattern.IsMatch(reasonMsg))
                    {
                        throw new Exception(
                            $"PBT-03: FailureReason.Message contém e-mail em claro. " +
                            $"Reason='{reasonMsg}'. E-mail do destinatário não deve aparecer em mensagens de erro.");
                    }
                }

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    // -------------------------------------------------------------------------
    // PBT-03C: EmailHasher.Hash nunca retorna o e-mail original em claro
    // -------------------------------------------------------------------------

    /// <summary>
    /// PBT-03C: para qualquer endereço de e-mail válido,
    /// <see cref="EmailHasher.Hash"/> retorna um valor que não contém o e-mail original.
    ///
    /// Garante a propriedade fundamental do hash usado no mascaramento (DD-008, RNF 4, PBT-03).
    ///
    /// ≥ 500 exemplos.
    /// </summary>
    [Fact(DisplayName = "PBT-03C: EmailHasher.Hash nunca retorna o e-mail original (≥500 exemplos)")]
    public void PbtAntiPii_EmailHasher_NeverReturnsOriginalEmail()
    {
        var prop = Prop.ForAll(
            EmailMessageArbitraries.ValidEmailAddress(),
            email =>
            {
                // Act
                var hashed = EmailHasher.Hash(email);

                // Assert — o hash não deve conter o e-mail original em claro
                if (hashed.Contains(email, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(
                        $"PBT-03C: EmailHasher.Hash retornou valor que contém e-mail original. " +
                        $"Email='{email}' Hash='{hashed}'.");
                }

                // O hash deve ter o prefixo "email#" (DD-008)
                if (!hashed.StartsWith("email#", StringComparison.Ordinal))
                {
                    throw new Exception(
                        $"PBT-03C: EmailHasher.Hash não tem prefixo 'email#'. Hash='{hashed}'.");
                }

                // O hash não deve casar o padrão de e-mail (RNF 4)
                if (EmailPattern.IsMatch(hashed))
                {
                    throw new Exception(
                        $"PBT-03C: EmailHasher.Hash parece ser um e-mail em claro. Hash='{hashed}'.");
                }

                // IsHashed deve retornar true para o resultado
                if (!EmailHasher.IsHashed(hashed))
                {
                    throw new Exception(
                        $"PBT-03C: EmailHasher.IsHashed retornou false para resultado de Hash. Hash='{hashed}'.");
                }

                return true;
            });

        Check.One(FsCheckConfig, prop);
    }

    // -------------------------------------------------------------------------
    // Helper: varredura de logs por PII
    // -------------------------------------------------------------------------

    /// <summary>
    /// Varre todos os eventos de log do <paramref name="sink"/> e lança exceção
    /// se qualquer evento contiver o <paramref name="email"/> em claro.
    ///
    /// A varredura inclui a mensagem renderizada e todas as propriedades de log.
    /// </summary>
    private static void AssertNoEmailInLogs(InMemorySink sink, string email)
    {
        var logEvents = sink.LogEvents.ToList();

        foreach (var logEvent in logEvents)
        {
            // Verifica a mensagem renderizada
            var rendered = logEvent.RenderMessage();
            if (rendered.Contains(email, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"PBT-03: log contém e-mail em claro na mensagem renderizada. " +
                    $"Email='{email}' Log='{rendered}'.");
            }

            // Verifica cada propriedade de log estruturado
            foreach (var (key, value) in logEvent.Properties)
            {
                var valueStr = value.ToString();
                if (valueStr.Contains(email, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(
                        $"PBT-03: log contém e-mail em claro na propriedade '{key}'. " +
                        $"Email='{email}' Valor='{valueStr}'.");
                }
            }

            // Varredura adicional com regex no texto completo do evento
            var fullText = logEvent.RenderMessage() + " " +
                string.Join(" ", logEvent.Properties.Values.Select(v => v.ToString()));

            if (EmailPattern.IsMatch(fullText) && fullText.Contains(email, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"PBT-03: padrão de e-mail detectado em log (varredura regex). " +
                    $"Email='{email}' Texto='{fullText}'.");
            }
        }
    }
}
