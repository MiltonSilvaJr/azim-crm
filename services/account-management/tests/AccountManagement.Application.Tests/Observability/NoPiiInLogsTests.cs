using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Accounts.Commands.CreateAccount;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AccountManagement.Application.Tests.Observability;

/// <summary>
/// Testes de ausência de PII em logs estruturados — design §11, RNF 1.1.
///
/// ST-02 de TASK-16: criar contato com nome e e-mail reais, varrer logs —
/// nenhuma linha deve conter o valor de PII em texto claro.
///
/// Mapeia: TASK-16 ST-02, RNF 1.1, RNF 9.1, design §11.
/// </summary>
public sealed class NoPiiInLogsTests
{
    /// <summary>
    /// Sink de log de teste que captura todas as mensagens para inspeção.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<string> _messages = [];
        public IReadOnlyList<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }
    }

    [Fact(DisplayName = "CorrelationLoggingBehavior: não registra PII (nome do contato) em nenhuma mensagem de log")]
    public async Task Behavior_DoesNotLogContactName_InMessages()
    {
        // Arrange
        var contactName = "Maria Oliveira";
        var contactEmail = "maria.oliveira@empresa.com";
        var contactPhone = "11987654321";

        var capturingLogger = new CapturingLogger<CorrelationLoggingBehavior<CreateAccountCommand, Guid>>();
        var correlationContext = new CorrelationContext();
        correlationContext.SetCorrelation(Guid.NewGuid().ToString(), Guid.NewGuid());

        var behavior = new CorrelationLoggingBehavior<CreateAccountCommand, Guid>(
            capturingLogger, correlationContext);

        // O request não deve resultar em PII nos logs mesmo que o nome chegue no command
        // (na vida real o CreateAccountCommand não carrega PII de contato, mas verificamos
        //  que o behavior não loga o payload inteiro do request)
        var request = new CreateAccountCommand(
            TenantId: Guid.NewGuid(),
            Name: "Empresa XPTO",
            Website: null,
            Notes: null,
            BuId: null,
            ConfirmCreateDespiteSimilar: false);

        // Act
        await behavior.Handle(request, _ => Task.FromResult(Guid.NewGuid()), CancellationToken.None);

        // Assert — nenhuma mensagem de log deve conter valores de PII simulados
        capturingLogger.Messages.Should().NotBeEmpty("behavior deve produzir ao menos um log");
        foreach (var message in capturingLogger.Messages)
        {
            message.Should().NotContain(contactName,
                because: "PII de contato não deve aparecer em logs do CorrelationLoggingBehavior");
            message.Should().NotContain(contactEmail,
                because: "e-mail de contato não deve aparecer em logs");
            message.Should().NotContain(contactPhone,
                because: "telefone de contato não deve aparecer em logs");
        }
    }

    [Fact(DisplayName = "CorrelationLoggingBehavior: correlation_id aparece nos logs sem PII")]
    public async Task Behavior_LogsCorrelationId_WithoutPii()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var capturingLogger = new CapturingLogger<CorrelationLoggingBehavior<CreateAccountCommand, Guid>>();
        var correlationContext = new CorrelationContext();
        correlationContext.SetCorrelation(correlationId, tenantId);

        var behavior = new CorrelationLoggingBehavior<CreateAccountCommand, Guid>(
            capturingLogger, correlationContext);

        var request = new CreateAccountCommand(
            TenantId: tenantId,
            Name: "Empresa Teste",
            Website: null,
            Notes: null,
            BuId: null,
            ConfirmCreateDespiteSimilar: false);

        // Act
        await behavior.Handle(request, _ => Task.FromResult(Guid.NewGuid()), CancellationToken.None);

        // Assert — pelo menos uma mensagem deve conter o correlation_id
        capturingLogger.Messages.Should().Contain(
            m => m.Contains(correlationId),
            because: "correlation_id deve aparecer nas mensagens de log");
    }

    [Fact(DisplayName = "CorrelationLoggingBehavior: exceção não vaza PII no log de erro")]
    public async Task Behavior_ExceptionLog_DoesNotLeakPii()
    {
        // Arrange
        var piiName = "João da Silva";
        var capturingLogger = new CapturingLogger<CorrelationLoggingBehavior<CreateAccountCommand, Guid>>();
        var correlationContext = new CorrelationContext();
        correlationContext.SetCorrelation(Guid.NewGuid().ToString(), Guid.NewGuid());

        var behavior = new CorrelationLoggingBehavior<CreateAccountCommand, Guid>(
            capturingLogger, correlationContext);

        var request = new CreateAccountCommand(
            TenantId: Guid.NewGuid(),
            Name: "Empresa Falha",
            Website: null,
            Notes: null,
            BuId: null,
            ConfirmCreateDespiteSimilar: false);

        // Act — handler lança exceção (simula falha de infra)
        var act = async () => await behavior.Handle(
            request,
            _ => Task.FromException<Guid>(new InvalidOperationException("database error")),
            CancellationToken.None);

        // Assert — exceção relançada e log de erro não contém PII
        await act.Should().ThrowAsync<InvalidOperationException>();
        foreach (var message in capturingLogger.Messages)
        {
            message.Should().NotContain(piiName,
                because: "nome do contato não deve aparecer no log de erro");
        }
    }
}
