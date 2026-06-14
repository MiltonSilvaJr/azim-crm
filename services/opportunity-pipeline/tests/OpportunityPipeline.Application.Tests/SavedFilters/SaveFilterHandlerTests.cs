using FluentAssertions;
using NSubstitute;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.SavedFilters;
using Xunit;

namespace OpportunityPipeline.Application.Tests.SavedFilters;

/// <summary>
/// Testes do SaveFilterHandler.
/// Cobre: ST-05 unicidade nome por usuário/tenant; persiste criteria JSON.
/// Mapeia: Req 19, design §5.3, TASK-11 ST-05.
/// </summary>
public sealed class SaveFilterHandlerTests
{
    private readonly ISavedFilterRepository _filterRepo = Substitute.For<ISavedFilterRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private TenantContext BuildContext()
    {
        var ctx = new TenantContext();
        ctx.Initialize(_tenantId, _buId, _actorId);
        return ctx;
    }

    private SaveFilterHandler BuildHandler() =>
        new(_filterRepo, BuildContext(), _uow);

    [Fact]
    public async Task SaveFilter_novo_nome_persiste_e_retorna_id()
    {
        // Cenário base: nome único → persiste com sucesso
        _filterRepo.ExistsByNameAsync(_tenantId, _actorId, "Meu Filtro", Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = BuildHandler();
        var command = new SaveFilterCommand
        {
            CorrelationId = "test",
            Name = "Meu Filtro",
            CriteriaJson = """{"stage":"Open","owner_id":"abc"}"""
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.FilterId.Should().NotBeEmpty();
        result.Name.Should().Be("Meu Filtro");
        await _filterRepo.Received(1).AddAsync(
            Arg.Is<SavedFilter>(f =>
                f.Name == "Meu Filtro" &&
                f.TenantId == _tenantId &&
                f.UserId == _actorId &&
                f.CriteriaJson.Contains("Open")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveFilter_nome_duplicado_lanca_ValidationException_422()
    {
        // Nome já existe para o mesmo usuário/tenant → 422
        _filterRepo.ExistsByNameAsync(_tenantId, _actorId, "Filtro Existente", Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = BuildHandler();
        var command = new SaveFilterCommand
        {
            CorrelationId = "test",
            Name = "Filtro Existente",
            CriteriaJson = """{"stage":"Open"}"""
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey("name"));
    }

    [Fact]
    public async Task SaveFilter_registra_auditoria()
    {
        // Verifica que RegisterAudit é chamado após persistir
        _filterRepo.ExistsByNameAsync(_tenantId, _actorId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = BuildHandler();
        var command = new SaveFilterCommand
        {
            CorrelationId = "test",
            Name = "Filtro Auditado",
            CriteriaJson = """{"stage":"Open"}"""
        };

        await handler.Handle(command, CancellationToken.None);

        _uow.Received(1).RegisterAudit(
            Arg.Any<Guid>(),
            "SavedFilter",
            Arg.Any<object>());
    }

    [Fact]
    public async Task SaveFilter_validator_rejeita_nome_vazio()
    {
        var validator = new SaveFilterValidator();
        var command = new SaveFilterCommand
        {
            CorrelationId = "test",
            Name = "",
            CriteriaJson = """{"stage":"Open"}"""
        };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task SaveFilter_validator_rejeita_criteria_json_vazio()
    {
        var validator = new SaveFilterValidator();
        var command = new SaveFilterCommand
        {
            CorrelationId = "test",
            Name = "Filtro Válido",
            CriteriaJson = ""
        };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CriteriaJson");
    }

    [Fact]
    public async Task SaveFilter_validator_rejeita_nome_excedendo_100_caracteres()
    {
        var validator = new SaveFilterValidator();
        var command = new SaveFilterCommand
        {
            CorrelationId = "test",
            Name = new string('A', 101),
            CriteriaJson = """{"stage":"Open"}"""
        };

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }
}
