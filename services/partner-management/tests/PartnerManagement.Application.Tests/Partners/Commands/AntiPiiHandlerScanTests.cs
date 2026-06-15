using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PartnerManagement.Application.Partners.Commands;
using PartnerManagement.Application.Ports;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.Repositories;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Application.Tests.Partners.Commands;

/// <summary>
/// Scan anti-PII para handlers — gate CI obrigatório (categoria <c>PiiScan</c>).
/// Executa o fluxo de criação de parceiro e verifica que nenhum log emitido pelo handler
/// contém <c>contact_email</c> nem <c>contact_phone</c> em texto claro.
/// <c>partner.name</c> <strong>não é PII</strong> por decisão VAL-PARTNER-01 (2026-06-15):
/// sua presença em logs não é violação e não é verificada aqui.
/// Usa <see cref="CaptureLogger{T}"/> para capturar mensagens do <see cref="ILogger{TCategoryName}"/>.
/// Verificação direta por substring (sem depender de PartnerPiiMasker da camada Infrastructure).
///
/// Mapeia: TASK-27, RNF 4.1, design §11, RISK-PM-02.
/// </summary>
[Trait("Category", "PiiScan")]
public sealed class AntiPiiHandlerScanTests
{
    // PII de teste com valores realistas
    private const string KnownEmail = "maria.aparecida@empresa.com.br";
    private const string KnownPhone = "11988887777";

    // =========================================================================
    // TASK-27: CreatePartnerHandler não loga PII de contato em claro
    // =========================================================================

    [Fact(DisplayName = "TASK-27: CreatePartnerHandler não emite PII de contato em logs")]
    public async Task CreatePartner_DoesNotEmitContactPiiInLogs()
    {
        // Arrange
        CaptureLogger<CreatePartnerHandler> captureLogger = new();
        IPartnerRepository repo = Substitute.For<IPartnerRepository>();
        ICanonicalRoleProvider roleProvider = Substitute.For<ICanonicalRoleProvider>();
        IPartnerMetrics metrics = Substitute.For<IPartnerMetrics>();

        roleProvider.IsCanonical(Arg.Any<string>(), Arg.Any<Guid>()).Returns(true);
        repo.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PartnerManagement.Domain.Partners.Partner>());

        CreatePartnerHandler handler = new(repo, roleProvider, metrics, captureLogger);

        CreatePartnerCommand command = new(
            TenantId: Guid.NewGuid(),
            Name: "Maria Aparecida da Silva",
            Role: "Indicador",
            CommissionDefaults: CommissionDefaults.Create(
                Percentage.Create(10.00m),
                Percentage.Create(5.00m)),
            ContactEmail: KnownEmail,
            ContactPhone: KnownPhone,
            Notes: "Parceiro de teste",
            CreatedBy: Guid.NewGuid(),
            ConfirmCreateDespiteDuplicate: false);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — nenhum log deve conter PII de contato em claro
        // (name não é PII por VAL-PARTNER-01 e não é verificado)
        foreach (string message in captureLogger.Messages)
        {
            message.Should().NotContain(KnownEmail,
                $"log '{Truncate(message, 120)}' contém e-mail em claro — violação de RNF 4.1");
            message.Should().NotContain(KnownPhone,
                $"log '{Truncate(message, 120)}' contém telefone em claro — violação de RNF 4.1");
        }
    }

    // Utilitário: trunca mensagem longa para exibição em output de teste
    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}

// =========================================================================
// CaptureLogger — captura mensagens de ILogger para inspeção nos testes
// =========================================================================

/// <summary>
/// Implementação de <see cref="ILogger{TCategoryName}"/> que captura todas as mensagens
/// emitidas durante os testes. Usado pelo gate anti-PII para inspecionar logs do handler.
/// </summary>
/// <typeparam name="T">Categoria do logger (tipo do handler).</typeparam>
internal sealed class CaptureLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];

    /// <summary>Mensagens capturadas pelos logs emitidos durante o teste.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        if (!string.IsNullOrEmpty(message))
        {
            _messages.Add(message);
        }
    }
}
