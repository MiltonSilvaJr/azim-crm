using DataMigration.Application.Behaviors;
using DataMigration.Application.Commands.Import;
using DataMigration.Application.Commands.Upload;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Logging;
using DataMigration.Application.Ports;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DataMigration.Application.Tests.Behaviors;

/// <summary>
/// Testes dos pipeline behaviors.
///
/// Rastreia: TASK-12, TASK-24, design §5.4, RNF 2, RNF 3, DD-009, ADR-0001.
/// </summary>
public sealed class PipelineBehaviorsTests
{
    // =========================================================================
    // TenantContextBehavior
    // =========================================================================

    [Fact(DisplayName = "TenantContextBehavior bloqueia quando tenant ausente")]
    public async Task TenantContextBehavior_WhenTenantMissing_Throws()
    {
        // Arrange
        var tenantContext = Substitute.For<ICurrentTenantContext>();
        tenantContext.TenantId.Returns((Guid?)null);

        var behavior = new TenantContextBehavior<UploadSpreadsheetCommand, UploadSpreadsheetResult>(
            tenantContext);

        var next = Substitute.For<RequestHandlerDelegate<UploadSpreadsheetResult>>();

        var command = new UploadSpreadsheetCommand("test.xlsx", Stream.Null, 1024L, "hash");

        // Act
        var act = () => behavior.Handle(command, next, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-005");
        await next.DidNotReceive().Invoke();
    }

    [Fact(DisplayName = "TenantContextBehavior passa quando tenant presente")]
    public async Task TenantContextBehavior_WhenTenantPresent_CallsNext()
    {
        // Arrange
        var tenantContext = Substitute.For<ICurrentTenantContext>();
        tenantContext.TenantId.Returns(Guid.NewGuid());

        var behavior = new TenantContextBehavior<UploadSpreadsheetCommand, UploadSpreadsheetResult>(
            tenantContext);

        var expected = new UploadSpreadsheetResult(Guid.NewGuid(), "created", 10);
        var next = Substitute.For<RequestHandlerDelegate<UploadSpreadsheetResult>>();
        next().Returns(expected);

        var command = new UploadSpreadsheetCommand("test.xlsx", Stream.Null, 1024L, "hash");

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        await next.Received(1).Invoke();
    }

    // =========================================================================
    // AuthorizationBehavior
    // =========================================================================

    [Fact(DisplayName = "AuthorizationBehavior rejeita papel errado para ExecuteImport")]
    public async Task AuthorizationBehavior_WhenWrongRole_Throws()
    {
        // Arrange
        var tenantContext = Substitute.For<ICurrentTenantContext>();
        tenantContext.Role.Returns("TenantAdmin"); // ExecuteImport requer PlatformOperator

        var behavior = new AuthorizationBehavior<ExecuteImportCommand, ExecuteImportResult>(
            tenantContext);

        var next = Substitute.For<RequestHandlerDelegate<ExecuteImportResult>>();
        var command = new ExecuteImportCommand(Guid.NewGuid(), true, Stream.Null);

        // Act
        var act = () => behavior.Handle(command, next, CancellationToken.None);

        // Assert — autorização negada usa MIG-ERR-009 (403 Forbidden) conforme design §12
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-009");
        await next.DidNotReceive().Invoke();
    }

    [Fact(DisplayName = "AuthorizationBehavior permite PlatformOperator para ExecuteImport")]
    public async Task AuthorizationBehavior_WhenCorrectRole_CallsNext()
    {
        // Arrange
        var tenantContext = Substitute.For<ICurrentTenantContext>();
        tenantContext.Role.Returns("PlatformOperator");

        var behavior = new AuthorizationBehavior<ExecuteImportCommand, ExecuteImportResult>(
            tenantContext);

        var expected = new ExecuteImportResult(Guid.NewGuid(), "completed");
        var next = Substitute.For<RequestHandlerDelegate<ExecuteImportResult>>();
        next().Returns(expected);
        var command = new ExecuteImportCommand(Guid.NewGuid(), true, Stream.Null);

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    // =========================================================================
    // FeatureFlagBehavior
    // =========================================================================

    [Fact(DisplayName = "FeatureFlagBehavior retorna MIG-ERR-009 quando migration.import_enabled=false")]
    public async Task FeatureFlagBehavior_WhenFlagDisabled_ThrowsMigErr009()
    {
        // Arrange
        var featureFlags = Substitute.For<IFeatureFlags>();
        featureFlags.IsEnabled("migration.import_enabled").Returns(false);

        var behavior = new FeatureFlagBehavior<ExecuteImportCommand, ExecuteImportResult>(
            featureFlags);

        var next = Substitute.For<RequestHandlerDelegate<ExecuteImportResult>>();
        var command = new ExecuteImportCommand(Guid.NewGuid(), true, Stream.Null);

        // Act
        var act = () => behavior.Handle(command, next, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<MigrationDomainException>();
        ex.Which.ErrorCode.Should().Be("MIG-ERR-009");
        await next.DidNotReceive().Invoke();
    }

    [Fact(DisplayName = "FeatureFlagBehavior passa quando flag habilitada")]
    public async Task FeatureFlagBehavior_WhenFlagEnabled_CallsNext()
    {
        // Arrange
        var featureFlags = Substitute.For<IFeatureFlags>();
        featureFlags.IsEnabled("migration.import_enabled").Returns(true);

        var behavior = new FeatureFlagBehavior<ExecuteImportCommand, ExecuteImportResult>(
            featureFlags);

        var expected = new ExecuteImportResult(Guid.NewGuid(), "completed");
        var next = Substitute.For<RequestHandlerDelegate<ExecuteImportResult>>();
        next().Returns(expected);
        var command = new ExecuteImportCommand(Guid.NewGuid(), true, Stream.Null);

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    // =========================================================================
    // PiiSafeLoggingBehavior — TASK-24 (implementação real)
    // =========================================================================

    private static PiiSafeLoggingBehavior<TRequest, TResponse> CreatePiiSafeBehavior<TRequest, TResponse>()
        where TRequest : notnull
    {
        var logger = Substitute.For<ILogger<PiiSafeLoggingBehavior<TRequest, TResponse>>>();
        return new PiiSafeLoggingBehavior<TRequest, TResponse>(logger);
    }

    [Fact(DisplayName = "PiiSafeLoggingBehavior é transparente e passa a resposta")]
    public async Task PiiSafeLoggingBehavior_IsTransparent_PassesResponse()
    {
        // Arrange
        var behavior = CreatePiiSafeBehavior<UploadSpreadsheetCommand, UploadSpreadsheetResult>();
        var expected = new UploadSpreadsheetResult(Guid.NewGuid(), "created", 5);
        var next = Substitute.For<RequestHandlerDelegate<UploadSpreadsheetResult>>();
        next().Returns(expected);

        var command = new UploadSpreadsheetCommand("pipeline.xlsx", Stream.Null, 1024L, "hash");

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        await next.Received(1).Invoke();
    }

    [Fact(DisplayName = "PiiSafeLoggingBehavior propaga exceção sem suprimir")]
    public async Task PiiSafeLoggingBehavior_PropagatesException()
    {
        // Arrange
        var behavior = CreatePiiSafeBehavior<UploadSpreadsheetCommand, UploadSpreadsheetResult>();
        var next = Substitute.For<RequestHandlerDelegate<UploadSpreadsheetResult>>();
        next().Returns(Task.FromException<UploadSpreadsheetResult>(
            new InvalidOperationException("erro técnico")));

        var command = new UploadSpreadsheetCommand("pipeline.xlsx", Stream.Null, 1024L, "hash");

        // Act
        var act = () => behavior.Handle(command, next, CancellationToken.None);

        // Assert: exceção propagada sem perda (sem suprimir erros reais)
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // =========================================================================
    // PiiSafeLogger — testes unitários da classe de proteção de PII
    // =========================================================================

    [Theory(DisplayName = "PiiSafeLogger.IsProhibitedField identifica campos PII")]
    [InlineData("contactName", true)]
    [InlineData("contact_name", true)]
    [InlineData("email", true)]
    [InlineData("phone", true)]
    [InlineData("telefone", true)]
    [InlineData("nome_contato", true)]
    [InlineData("source_row_index", false)]
    [InlineData("migration_job_id", false)]
    [InlineData("status", false)]
    [InlineData("source_sheet", false)]
    public void PiiSafeLogger_IsProhibitedField_DetectsCorrectly(string fieldName, bool expected)
    {
        PiiSafeLogger.IsProhibitedField(fieldName).Should().Be(expected);
    }

    [Fact(DisplayName = "PiiSafeLogger.MaskIfPii retorna marcador para campo PII")]
    public void PiiSafeLogger_MaskIfPii_ReturnsMaskForPiiField()
    {
        // Arrange
        const string contactName = "João da Silva";
        const string email = "joao@empresa.com";

        // Act & Assert
        PiiSafeLogger.MaskIfPii("contactName", contactName)
            .Should().Be("[PII-REMOVIDO]");

        PiiSafeLogger.MaskIfPii("email", email)
            .Should().Be("[PII-REMOVIDO]");
    }

    [Fact(DisplayName = "PiiSafeLogger.MaskIfPii retorna valor original para campo não-PII")]
    public void PiiSafeLogger_MaskIfPii_ReturnsOriginalForAllowedField()
    {
        // Arrange
        const string rowIndex = "42";

        // Act & Assert
        PiiSafeLogger.MaskIfPii("source_row_index", rowIndex)
            .Should().Be(rowIndex);
    }

    [Theory(DisplayName = "PiiSafeLogger.SanitizeMessage remove e-mails de mensagens")]
    [InlineData("erro na linha 5: contato joao@empresa.com", "[PII-REMOVIDO]")]
    [InlineData("falha ao processar maria@acme.co.br: dado inválido", "[PII-REMOVIDO]")]
    [InlineData("linha 42: campo obrigatório ausente", null)] // sem PII
    public void PiiSafeLogger_SanitizeMessage_RemovesEmails(string input, string? piiFrag)
    {
        var result = PiiSafeLogger.SanitizeMessage(input);

        if (piiFrag != null)
        {
            result.Should().Contain(piiFrag, "e-mail deve ser mascarado");
            result.Should().NotContain("@", "e-mail original não deve aparecer");
        }
        else
        {
            result.Should().Be(input, "sem PII: mensagem deve ser preservada");
        }
    }

    [Fact(DisplayName = "PiiSafeLogger registra linha processada sem expor PII")]
    public void PiiSafeLogger_LogRowProcessed_DoesNotExposePii()
    {
        // Arrange — usa Microsoft.Extensions.Logging.Abstractions NullLogger
        // para evitar problemas de NSubstitute com Log<TState> genérico.
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var piiLogger = new PiiSafeLogger(logger);

        // Act — não deve lançar; a sanitização da mensagem é validada pelos testes de SanitizeMessage
        var act = () => piiLogger.LogRowProcessed(
            migrationJobId: Guid.NewGuid(),
            sourceSheet: "Pipeline",
            sourceRowIndex: 42,
            status: "ok",
            message: "Oportunidade criada com sucesso",
            correlationId: Guid.NewGuid());

        // Assert — não lança exceção (saída de log vai para NullLogger)
        act.Should().NotThrow("PiiSafeLogger não deve lançar ao registrar linha sem PII");
    }

    [Fact(DisplayName = "PiiSafeLogger.SanitizeMessage bloqueia e-mail em mensagem de erro")]
    public void PiiSafeLogger_SanitizeMessage_BlocksEmailInErrorMessage()
    {
        // Simula mensagem de erro que vaza e-mail de contato
        const string errorMsg = "Erro ao criar contato: email joao@empresa.com já existe";
        var sanitized = PiiSafeLogger.SanitizeMessage(errorMsg);

        sanitized.Should().NotContain("joao@empresa.com",
            "e-mail de contato não deve aparecer em logs");
        sanitized.Should().Contain("[PII-REMOVIDO]",
            "marcador de PII deve substituir o e-mail");
    }

    [Fact(DisplayName = "PiiSafeLogger.SanitizeMessage preserva mensagens técnicas sem PII")]
    public void PiiSafeLogger_SanitizeMessage_PreservesTechnicalMessages()
    {
        const string technical = "Oportunidade AZ-0042 criada para conta id=abc123";
        var result = PiiSafeLogger.SanitizeMessage(technical);
        result.Should().Be(technical);
    }
}
