using FluentValidation;
using GoalForecast.Application.Behaviors;
using GoalForecast.Application.Commands;
using GoalForecast.Domain.Authorization;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Tests.Behaviors;

/// <summary>
/// Testes dos cinco pipeline behaviors em isolamento.
/// Mapeia: TASK-10, design §5.4.
/// </summary>
public sealed class BehaviorsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();

    private static readonly GoalPrincipal ValidPrincipal = new(
        TenantId: TenantId,
        UserId: Guid.NewGuid(),
        Role: GoalRole.TenantAdmin,
        BuId: null);

    // --- LoggingBehavior ---

    [Fact]
    public async Task LoggingBehavior_deve_chamar_next_e_retornar_resposta()
    {
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(
            NullLogger<LoggingBehavior<TestRequest, TestResponse>>.Instance);

        var expected = new TestResponse("ok");
        var result = await behavior.Handle(
            new TestRequest(ValidPrincipal),
            _ => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task LoggingBehavior_deve_propagar_excecao_do_next()
    {
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(
            NullLogger<LoggingBehavior<TestRequest, TestResponse>>.Instance);

        var act = async () => await behavior.Handle(
            new TestRequest(ValidPrincipal),
            _ => throw new InvalidOperationException("erro"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // --- TenantContextBehavior ---

    [Fact]
    public async Task TenantContextBehavior_deve_passar_quando_tenant_valido()
    {
        var behavior = new TenantContextBehavior<TestRequest, TestResponse>();
        var expected = new TestResponse("ok");

        var result = await behavior.Handle(
            new TestRequest(ValidPrincipal),
            _ => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task TenantContextBehavior_deve_lancar_quando_tenant_empty()
    {
        var behavior = new TenantContextBehavior<TestRequest, TestResponse>();
        var emptyTenantPrincipal = new GoalPrincipal(
            TenantId: Guid.Empty,
            UserId: Guid.NewGuid(),
            Role: GoalRole.TenantAdmin,
            BuId: null);

        var act = async () => await behavior.Handle(
            new TestRequest(emptyTenantPrincipal),
            _ => Task.FromResult(new TestResponse("ok")),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("GF-ERR-006");
        ex.Which.SuggestedHttpStatus.Should().Be(403);
    }

    [Fact]
    public async Task TenantContextBehavior_deve_passar_request_sem_principal()
    {
        var behavior = new TenantContextBehavior<RequestSemPrincipal, TestResponse>();
        var result = await behavior.Handle(
            new RequestSemPrincipal(),
            _ => Task.FromResult(new TestResponse("ok")),
            CancellationToken.None);

        result.Value.Should().Be("ok");
    }

    // --- ValidationBehavior ---

    [Fact]
    public async Task ValidationBehavior_sem_validators_deve_chamar_next()
    {
        var behavior = new ValidationBehavior<TestRequest, TestResponse>(
            Enumerable.Empty<IValidator<TestRequest>>());

        var result = await behavior.Handle(
            new TestRequest(ValidPrincipal),
            _ => Task.FromResult(new TestResponse("ok")),
            CancellationToken.None);

        result.Value.Should().Be("ok");
    }

    [Fact]
    public async Task ValidationBehavior_com_command_valido_deve_chamar_next()
    {
        var validators = new IValidator<CreateOrUpdateGoalCommand>[]
        {
            new CreateOrUpdateGoalCommandValidator()
        };
        var behavior = new ValidationBehavior<CreateOrUpdateGoalCommand, CreateOrUpdateGoalResult>(validators);

        var command = new CreateOrUpdateGoalCommand
        {
            Principal = ValidPrincipal,
            Scope = "BU",
            BuId = BuId,
            OwnerId = null,
            Year = 2026,
            Month = 6,
            ValorMeta = 50_000L
        };

        var expected = new CreateOrUpdateGoalResult(null!, false);
        var result = await behavior.Handle(command, _ => Task.FromResult(expected), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task ValidationBehavior_com_mes_invalido_deve_lancar_ValidationException()
    {
        var validators = new IValidator<CreateOrUpdateGoalCommand>[]
        {
            new CreateOrUpdateGoalCommandValidator()
        };
        var behavior = new ValidationBehavior<CreateOrUpdateGoalCommand, CreateOrUpdateGoalResult>(validators);

        var command = new CreateOrUpdateGoalCommand
        {
            Principal = ValidPrincipal,
            Scope = "BU",
            BuId = BuId,
            Year = 2026,
            Month = 13,
            ValorMeta = 50_000L
        };

        var act = async () => await behavior.Handle(command,
            _ => Task.FromResult(new CreateOrUpdateGoalResult(null!, false)),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.ErrorCode == "GF-ERR-002");
    }

    [Fact]
    public async Task ValidationBehavior_com_valorMeta_negativo_deve_lancar_GF_ERR_001()
    {
        var validators = new IValidator<CreateOrUpdateGoalCommand>[]
        {
            new CreateOrUpdateGoalCommandValidator()
        };
        var behavior = new ValidationBehavior<CreateOrUpdateGoalCommand, CreateOrUpdateGoalResult>(validators);

        var command = new CreateOrUpdateGoalCommand
        {
            Principal = ValidPrincipal,
            Scope = "BU",
            BuId = BuId,
            Year = 2026,
            Month = 6,
            ValorMeta = -1L
        };

        var act = async () => await behavior.Handle(command,
            _ => Task.FromResult(new CreateOrUpdateGoalResult(null!, false)),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.ErrorCode == "GF-ERR-001");
    }

    [Fact]
    public async Task ValidationBehavior_responsavel_sem_ownerId_deve_lancar_GF_ERR_003()
    {
        var validators = new IValidator<CreateOrUpdateGoalCommand>[]
        {
            new CreateOrUpdateGoalCommandValidator()
        };
        var behavior = new ValidationBehavior<CreateOrUpdateGoalCommand, CreateOrUpdateGoalResult>(validators);

        var command = new CreateOrUpdateGoalCommand
        {
            Principal = ValidPrincipal,
            Scope = "RESPONSAVEL",
            BuId = BuId,
            OwnerId = null,
            Year = 2026,
            Month = 6,
            ValorMeta = 50_000L
        };

        var act = async () => await behavior.Handle(command,
            _ => Task.FromResult(new CreateOrUpdateGoalResult(null!, false)),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e => e.ErrorCode == "GF-ERR-003");
    }

    // --- GoalAuthorizationBehavior ---

    [Fact]
    public async Task GoalAuthorizationBehavior_com_tenant_valido_deve_chamar_next()
    {
        var behavior = new GoalAuthorizationBehavior<TestRequest, TestResponse>();
        var expected = new TestResponse("ok");

        var result = await behavior.Handle(
            new TestRequest(ValidPrincipal),
            _ => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GoalAuthorizationBehavior_com_tenant_empty_deve_lancar_GF_ERR_006()
    {
        var behavior = new GoalAuthorizationBehavior<TestRequest, TestResponse>();
        var principal = new GoalPrincipal(Guid.Empty, Guid.NewGuid(), GoalRole.TenantAdmin, null);

        var act = async () => await behavior.Handle(
            new TestRequest(principal),
            _ => Task.FromResult(new TestResponse("ok")),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("GF-ERR-006");
    }

    // --- AuditBehavior ---

    [Fact]
    public async Task AuditBehavior_deve_chamar_next_e_retornar_resposta_em_sucesso()
    {
        var behavior = new AuditBehavior<TestRequest, TestResponse>(
            NullLogger<AuditBehavior<TestRequest, TestResponse>>.Instance);
        var expected = new TestResponse("ok");

        var result = await behavior.Handle(
            new TestRequest(ValidPrincipal),
            _ => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task AuditBehavior_nao_deve_engolir_excecao_do_handler()
    {
        var behavior = new AuditBehavior<TestRequest, TestResponse>(
            NullLogger<AuditBehavior<TestRequest, TestResponse>>.Instance);

        var act = async () => await behavior.Handle(
            new TestRequest(ValidPrincipal),
            _ => throw new InvalidOperationException("handler falhou"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("handler falhou");
    }
}

// --- Tipos auxiliares para testes ---

file sealed record TestRequest(GoalPrincipal Principal) : IHasPrincipal;
file sealed record TestResponse(string Value);
file sealed record RequestSemPrincipal;
