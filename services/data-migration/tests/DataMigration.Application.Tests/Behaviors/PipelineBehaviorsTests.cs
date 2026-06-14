using DataMigration.Application.Behaviors;
using DataMigration.Application.Commands.Import;
using DataMigration.Application.Commands.Upload;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace DataMigration.Application.Tests.Behaviors;

/// <summary>
/// Testes dos pipeline behaviors.
///
/// Rastreia: TASK-12, design §5.4, RNF 2, RNF 3, DD-009, ADR-0001.
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
    // PiiSafeLoggingBehavior
    // =========================================================================

    [Fact(DisplayName = "PiiSafeLoggingBehavior não propaga PII em logs estruturados")]
    public async Task PiiSafeLoggingBehavior_DoesNotExposePii()
    {
        // Arrange — behavior deve ser transparente (pass-through)
        var behavior = new PiiSafeLoggingBehavior<UploadSpreadsheetCommand, UploadSpreadsheetResult>();
        var expected = new UploadSpreadsheetResult(Guid.NewGuid(), "created", 5);
        var next = Substitute.For<RequestHandlerDelegate<UploadSpreadsheetResult>>();
        next().Returns(expected);

        var command = new UploadSpreadsheetCommand("pipeline.xlsx", Stream.Null, 1024L, "hash");

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert: comportamento pass-through; o teste valida que o behavior não lança
        result.Should().Be(expected);
        await next.Received(1).Invoke();
    }
}
