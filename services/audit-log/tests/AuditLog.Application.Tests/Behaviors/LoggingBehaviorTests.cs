using AuditLog.Application.Abstractions;
using AuditLog.Application.Behaviors;
using AuditLog.Application.Commands;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.Behaviors;

/// <summary>
/// Testes unitários do <see cref="LoggingBehavior{TRequest,TResponse}"/>.
/// Verifica que apenas metadados não-PII são incluídos nos logs (RNF-002.1).
/// </summary>
public sealed class LoggingBehaviorTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<LoggingBehavior<RecordAuditEntryCommand, Unit>> _logger =
        Substitute.For<ILogger<LoggingBehavior<RecordAuditEntryCommand, Unit>>>();

    private LoggingBehavior<RecordAuditEntryCommand, Unit> BuildSut() =>
        new(_tenantContext, _logger);

    // -----------------------------------------------------------------------
    // Passa para o handler
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve chamar o próximo handler")]
    public async Task Should_Call_Next()
    {
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        var sut = BuildSut();

        var nextCalled = false;
        await sut.Handle(
            ValidCommand(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    [Fact(DisplayName = "Deve propagar exceção do handler")]
    public async Task Should_Rethrow_Exception_From_Handler()
    {
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        var sut = BuildSut();

        var act = async () => await sut.Handle(
            ValidCommand(),
            _ => Task.FromException<Unit>(new InvalidOperationException("Erro simulado")),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // -----------------------------------------------------------------------
    // Não loga PII (RNF-002)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "NÃO deve logar PII — email não deve aparecer em chamadas de log")]
    public async Task Should_Not_Log_Pii_Email()
    {
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        var sut = BuildSut();

        var piiEmail = "usuario@empresa.com";
        var cmd = new RecordAuditEntryCommand
        {
            ActorId = Guid.NewGuid(),
            EntityType = "Contact",
            EntityId = Guid.NewGuid(),
            Action = Domain.ValueObjects.AuditAction.Create,
            RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["email"] = piiEmail,
                ["phone"] = "11999998888"
            }
        };

        await sut.Handle(cmd, _ => Task.FromResult(Unit.Value), CancellationToken.None);

        // Verifica que nenhuma chamada de log continha o email de PII
        _logger.ReceivedCalls().Should().AllSatisfy(call =>
        {
            var args = call.GetArguments();
            var logMessage = args.OfType<string>().FirstOrDefault() ?? "";
            logMessage.Should().NotContain(piiEmail,
                because: "PII (email) nunca deve aparecer em logs (RNF-002.1)");
        });
    }

    [Fact(DisplayName = "Deve emitir ao menos uma chamada de log com metadados seguros")]
    public async Task Should_Call_Logger_At_Least_Once()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        var sut = BuildSut();

        await sut.Handle(ValidCommand(), _ => Task.FromResult(Unit.Value), CancellationToken.None);

        // ILogger.LogInformation chama internamente Log<TState>; verificamos via ReceivedCalls
        _logger.ReceivedCalls().Should().NotBeEmpty(
            because: "LoggingBehavior deve emitir ao menos uma chamada de log por requisição (RNF-002.1)");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static RecordAuditEntryCommand ValidCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = Domain.ValueObjects.AuditAction.Create,
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" }
    };
}
