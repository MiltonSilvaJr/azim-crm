using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using GoalForecast.Application.Commands;
using GoalForecast.Application.Ports;
using GoalForecast.Domain.Aggregates;
using GoalForecast.Domain.Authorization;
using GoalForecast.Domain.Exceptions;
using GoalForecast.Domain.ValueObjects;
using NSubstitute;
using AppException = GoalForecast.Application.Common.ApplicationException;

namespace GoalForecast.Application.Tests.Commands;

/// <summary>
/// Testes do <see cref="CreateOrUpdateGoalCommandHandler"/>.
/// Inclui PBT-01: N upserts com mesma chave → 1 registro com último valorMeta.
/// Mapeia: TASK-09, Req 1, Req 2, Req 4, PBT-01.
/// </summary>
public sealed class CreateOrUpdateGoalCommandHandlerTests
{
    private readonly IGoalRepository _repository;
    private readonly IBuMembershipReader _membership;
    private readonly CreateOrUpdateGoalCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BuId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static readonly GoalPrincipal AdminPrincipal = new(
        TenantId: TenantId,
        UserId: Guid.NewGuid(),
        Role: GoalRole.TenantAdmin,
        BuId: null);

    private static readonly GoalPrincipal GestorPrincipal = new(
        TenantId: TenantId,
        UserId: Guid.NewGuid(),
        Role: GoalRole.GestorDeBu,
        BuId: BuId);

    public CreateOrUpdateGoalCommandHandlerTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _membership = Substitute.For<IBuMembershipReader>();
        _handler = new CreateOrUpdateGoalCommandHandler(_repository, _membership);
    }

    // --- Cenário 1: criação de nova meta (repositório retorna null) ---

    [Fact]
    public async Task Handle_quando_meta_nao_existe_deve_criar_novo_aggregate()
    {
        // Arrange
        _repository.FindByKey(TenantId, BuId, null, 2026, 6, default)
            .Returns((Goal?)null);

        var command = BuildCommand(AdminPrincipal, "BU", null, 2026, 6, 50_000L);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Created.Should().BeTrue();
        result.Goal.ValorMeta.Should().Be(50_000L);
        result.Goal.TenantId.Should().Be(TenantId);
        await _repository.Received(1).Add(Arg.Any<Goal>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().Update(Arg.Any<Goal>(), Arg.Any<CancellationToken>());
    }

    // --- Cenário 2: atualização de meta existente ---

    [Fact]
    public async Task Handle_quando_meta_existe_deve_atualizar_valorMeta()
    {
        // Arrange
        var existing = Goal.Create(TenantId, GoalScope.ForBu(BuId),
            new GoalPeriod(2026, 6), Money.Of(10_000L));

        _repository.FindByKey(TenantId, BuId, null, 2026, 6, default)
            .Returns(existing);

        var command = BuildCommand(AdminPrincipal, "BU", null, 2026, 6, 90_000L);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Created.Should().BeFalse();
        result.Goal.ValorMeta.Should().Be(90_000L);
        await _repository.Received(1).Update(Arg.Any<Goal>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().Add(Arg.Any<Goal>(), Arg.Any<CancellationToken>());
    }

    // --- Cenário 3: membership inválido lança GF-ERR-004 ---

    [Fact]
    public async Task Handle_quando_membership_invalido_deve_lancar_GF_ERR_004()
    {
        // Arrange
        _membership.IsOwnerMemberOfBu(TenantId, OwnerId, BuId, default).Returns(false);

        var command = BuildCommand(AdminPrincipal, "RESPONSAVEL", OwnerId, 2026, 6, 50_000L);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("GF-ERR-004");
        ex.Which.SuggestedHttpStatus.Should().Be(422);
        await _repository.DidNotReceive().Add(Arg.Any<Goal>(), Arg.Any<CancellationToken>());
    }

    // --- Cenário 4: autorização negada lança GF-ERR-006 ---

    [Fact]
    public async Task Handle_quando_autorizacao_negada_deve_lancar_GF_ERR_006()
    {
        // Arrange: Vendedor não pode escrever
        var vendedorPrincipal = new GoalPrincipal(
            TenantId: TenantId,
            UserId: OwnerId,
            Role: GoalRole.Vendedor,
            BuId: BuId);

        var command = BuildCommand(vendedorPrincipal, "BU", null, 2026, 6, 50_000L);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("GF-ERR-006");
        ex.Which.SuggestedHttpStatus.Should().Be(403);
        await _repository.DidNotReceive().Add(Arg.Any<Goal>(), Arg.Any<CancellationToken>());
    }

    // --- Cenário 5: Gestor não pode escrever em outra BU ---

    [Fact]
    public async Task Handle_gestor_nao_pode_escrever_em_outra_bu()
    {
        // Arrange
        var outraBu = Guid.NewGuid();
        var command = BuildCommand(GestorPrincipal, "BU", null, 2026, 6, 50_000L,
            buIdOverride: outraBu);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("GF-ERR-006");
    }

    // --- Cenário 6: escopo RESPONSAVEL com membership válido ---

    [Fact]
    public async Task Handle_responsavel_com_membership_valido_deve_criar()
    {
        // Arrange
        _membership.IsOwnerMemberOfBu(TenantId, OwnerId, BuId, default).Returns(true);
        _repository.FindByKey(TenantId, BuId, OwnerId, 2026, 6, default).Returns((Goal?)null);

        var command = BuildCommand(AdminPrincipal, "RESPONSAVEL", OwnerId, 2026, 6, 30_000L);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Created.Should().BeTrue();
        result.Goal.OwnerId.Should().Be(OwnerId);
        await _repository.Received(1).Add(Arg.Any<Goal>(), Arg.Any<CancellationToken>());
    }

    // --- Cenário 7: TenantId nunca aceito do payload ---

    [Fact]
    public async Task Handle_tenant_id_deve_vir_do_principal_nao_do_payload()
    {
        // Arrange
        _repository.FindByKey(TenantId, BuId, null, 2026, 6, default).Returns((Goal?)null);
        var command = BuildCommand(AdminPrincipal, "BU", null, 2026, 6, 100L);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: TenantId do dto deve ser o do principal
        result.Goal.TenantId.Should().Be(TenantId);
    }

    // --- PBT-01: N upserts com mesma chave → 1 registro com último valorMeta ---

    [Property(MaxTest = 200)]
    public Property PBT01_N_upserts_com_mesma_chave_resulta_em_ultimo_valorMeta()
    {
        return Prop.ForAll(
            ArbMap.Default.ArbFor<PositiveInt[]>().Filter(a => a.Length > 0 && a.Length <= 10),
            async (positiveInts) =>
            {
                // Arrange: criar mocks frescos por execução
                var repo = Substitute.For<IGoalRepository>();
                var membership = Substitute.For<IBuMembershipReader>();
                var handler = new CreateOrUpdateGoalCommandHandler(repo, membership);

                var centValues = positiveInts.Select(p => (long)p.Get).ToArray();
                Goal? lastPersistedGoal = null;

                // Simula: repositório retorna o goal mais recente (ou null na primeira chamada)
                repo.FindByKey(TenantId, BuId, null, 2026, 6, Arg.Any<CancellationToken>())
                    .Returns(ci => Task.FromResult(lastPersistedGoal));

                repo.When(r => r.Add(Arg.Any<Goal>(), Arg.Any<CancellationToken>()))
                    .Do(ci => lastPersistedGoal = ci.Arg<Goal>());

                repo.When(r => r.Update(Arg.Any<Goal>(), Arg.Any<CancellationToken>()))
                    .Do(ci => lastPersistedGoal = ci.Arg<Goal>());

                // Act: N upserts em sequência
                CreateOrUpdateGoalResult? lastResult = null;
                foreach (var cents in centValues)
                {
                    var command = BuildCommand(AdminPrincipal, "BU", null, 2026, 6, cents);
                    lastResult = await handler.Handle(command, CancellationToken.None);
                }

                // Assert: exatamente 1 registro com o último valor
                lastPersistedGoal.Should().NotBeNull();
                lastPersistedGoal!.ValorMeta.Cents.Should().Be(centValues[^1]);

                // Apenas 1 Add (criação) + N-1 Updates
                var totalCalls = await repo.ReceivedCalls()
                    .Where(c => c.GetMethodInfo().Name is "Add" or "Update")
                    .CountAsync();
                totalCalls.Should().Be(centValues.Length);
            });
    }

    // --- Helpers ---

    private static CreateOrUpdateGoalCommand BuildCommand(
        GoalPrincipal principal,
        string scope,
        Guid? ownerId,
        int year,
        int month,
        long valorMeta,
        Guid? buIdOverride = null,
        string currency = "BRL") =>
        new()
        {
            Principal = principal,
            Scope = scope,
            BuId = buIdOverride ?? BuId,
            OwnerId = ownerId,
            Year = year,
            Month = month,
            ValorMeta = valorMeta,
            Currency = currency
        };
}

// Helper extension para contar chamadas async
file static class CallExtensions
{
    public static async Task<int> CountAsync(this IEnumerable<NSubstitute.Core.ICall> calls)
    {
        await Task.CompletedTask;
        return calls.Count();
    }
}
