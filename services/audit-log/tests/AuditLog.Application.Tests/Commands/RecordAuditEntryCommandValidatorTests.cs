using AuditLog.Application.Commands;
using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace AuditLog.Application.Tests.Commands;

/// <summary>
/// Testes unitários do <see cref="RecordAuditEntryCommandValidator"/>.
/// Verifica rejeição de campos obrigatórios e regras por action.
/// </summary>
public sealed class RecordAuditEntryCommandValidatorTests
{
    private readonly RecordAuditEntryCommandValidator _sut = new();

    // -----------------------------------------------------------------------
    // ActorId
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve rejeitar ActorId vazio")]
    public void Should_Reject_Empty_ActorId()
    {
        var cmd = ValidCreateCommand() with { ActorId = Guid.Empty };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.ActorId);
    }

    [Fact(DisplayName = "Deve aceitar ActorId não vazio")]
    public void Should_Accept_NonEmpty_ActorId()
    {
        var result = _sut.TestValidate(ValidCreateCommand());
        result.ShouldNotHaveValidationErrorFor(x => x.ActorId);
    }

    // -----------------------------------------------------------------------
    // EntityType
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve rejeitar EntityType vazio")]
    public void Should_Reject_Empty_EntityType()
    {
        var cmd = ValidCreateCommand() with { EntityType = "" };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.EntityType);
    }

    [Fact(DisplayName = "Deve rejeitar EntityType com mais de 50 caracteres")]
    public void Should_Reject_EntityType_Over_50_Chars()
    {
        var cmd = ValidCreateCommand() with { EntityType = new string('X', 51) };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.EntityType);
    }

    [Fact(DisplayName = "Deve aceitar EntityType com exatamente 50 caracteres")]
    public void Should_Accept_EntityType_With_50_Chars()
    {
        var cmd = ValidCreateCommand() with { EntityType = new string('X', 50) };
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.EntityType);
    }

    // -----------------------------------------------------------------------
    // EntityId
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Deve rejeitar EntityId vazio")]
    public void Should_Reject_Empty_EntityId()
    {
        var cmd = ValidCreateCommand() with { EntityId = Guid.Empty };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.EntityId);
    }

    // -----------------------------------------------------------------------
    // Action
    // -----------------------------------------------------------------------

    [Theory(DisplayName = "Deve aceitar todos os valores canônicos de Action")]
    [InlineData(AuditAction.Create)]
    [InlineData(AuditAction.Update)]
    [InlineData(AuditAction.Delete)]
    public void Should_Accept_All_Canonical_Actions(AuditAction action)
    {
        var cmd = BuildCommandForAction(action);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Action);
    }

    [Fact(DisplayName = "Deve rejeitar valor inválido de Action")]
    public void Should_Reject_Invalid_Action()
    {
        var cmd = ValidCreateCommand() with { Action = (AuditAction)99 };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Action);
    }

    // -----------------------------------------------------------------------
    // Regras por action
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Para Create: deve rejeitar quando RawAfter é nulo")]
    public void Create_Should_Reject_Null_RawAfter()
    {
        var cmd = ValidCreateCommand() with { RawAfter = null };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RawAfter);
    }

    [Fact(DisplayName = "Para Create: deve rejeitar quando RawAfter está vazio")]
    public void Create_Should_Reject_Empty_RawAfter()
    {
        var cmd = ValidCreateCommand() with
        {
            RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal)
        };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RawAfter);
    }

    [Fact(DisplayName = "Para Delete: deve rejeitar quando RawBefore é nulo")]
    public void Delete_Should_Reject_Null_RawBefore()
    {
        var cmd = ValidDeleteCommand() with { RawBefore = null };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RawBefore);
    }

    [Fact(DisplayName = "Para Update: deve rejeitar quando RawBefore é nulo")]
    public void Update_Should_Reject_Null_RawBefore()
    {
        var cmd = ValidUpdateCommand() with { RawBefore = null };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RawBefore);
    }

    [Fact(DisplayName = "Para Update: deve rejeitar quando RawAfter é nulo")]
    public void Update_Should_Reject_Null_RawAfter()
    {
        var cmd = ValidUpdateCommand() with { RawAfter = null };
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RawAfter);
    }

    // -----------------------------------------------------------------------
    // Comando válido completo não gera erros
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Comando Create válido não deve gerar erros de validação")]
    public void Valid_Create_Command_Should_Have_No_Errors()
    {
        var result = _sut.TestValidate(ValidCreateCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando Update válido não deve gerar erros de validação")]
    public void Valid_Update_Command_Should_Have_No_Errors()
    {
        var result = _sut.TestValidate(ValidUpdateCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Comando Delete válido não deve gerar erros de validação")]
    public void Valid_Delete_Command_Should_Have_No_Errors()
    {
        var result = _sut.TestValidate(ValidDeleteCommand());
        result.IsValid.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static RecordAuditEntryCommand ValidCreateCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = AuditAction.Create,
        RawBefore = null,
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = "Oportunidade Teste",
            ["stage_id"] = Guid.NewGuid()
        }
    };

    private static RecordAuditEntryCommand ValidUpdateCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = AuditAction.Update,
        RawBefore = new Dictionary<string, object?>(StringComparer.Ordinal) { ["stage_id"] = Guid.NewGuid() },
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["stage_id"] = Guid.NewGuid() }
    };

    private static RecordAuditEntryCommand ValidDeleteCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = AuditAction.Delete,
        RawBefore = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Oportunidade Removida" },
        RawAfter = null
    };

    private static RecordAuditEntryCommand BuildCommandForAction(AuditAction action) =>
        action switch
        {
            AuditAction.Create => ValidCreateCommand(),
            AuditAction.Update => ValidUpdateCommand(),
            AuditAction.Delete => ValidDeleteCommand(),
            _ => ValidCreateCommand()
        };
}
