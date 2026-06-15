using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Application.Opportunities.Services;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Events;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.Repositories;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using Xunit;

namespace OpportunityPipeline.Application.Tests.Opportunities.Services;

/// <summary>
/// Testes do StagnationDetectionService.
/// Cobre: PBT-09 idempotência por (opportunity_id, detection_period);
/// detecção correta; degradação graciosa quando activity port indisponível.
/// Mapeia: Req 17, RNF 9, PBT-09, design §5.3, TASK-12 ST-01/07.
/// </summary>
public sealed class StagnationDetectionServiceTests
{
    private readonly IOpportunityRepository _repo = Substitute.For<IOpportunityRepository>();
    private readonly IOpportunityQueryRepository _queryRepo = Substitute.For<IOpportunityQueryRepository>();
    private readonly IActivityReadPort _activityPort = Substitute.For<IActivityReadPort>();
    private readonly IStaleDetectionRunRepository _detectionRunRepo = Substitute.For<IStaleDetectionRunRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Microsoft.Extensions.Logging.ILogger<StagnationDetectionService> _logger =
        Substitute.For<Microsoft.Extensions.Logging.ILogger<StagnationDetectionService>>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private StagnationDetectionService BuildService() =>
        new(_repo, _queryRepo, _activityPort, _detectionRunRepo, _uow, _clock, _logger);

    private static Opportunity BuildOpenOpportunity(Guid tenantId, Guid buId, Guid actorId)
    {
        var stage = new StageRef(Guid.NewGuid(), "Q", StageCategory.Open, 30, 1);
        var channel = new OriginChannelRef(Guid.NewGuid(), "Inbound", false);
        return Opportunity.Create(
            tenantId, buId, Guid.NewGuid(), actorId, null,
            stage, channel, "Opp Stagnation Test",
            new ContractValue(new Money(10000, "BRL"), new Money(0, "BRL"), 0),
            new Probability(50), null, null,
            new OpportunityNumber("AZ-0001"), actorId, DateTimeOffset.UtcNow);
    }

    // =========================================================================
    // PBT-09 — Idempotência de estagnação
    // =========================================================================

    /// <summary>
    /// PBT-09: Para qualquer N ≥ 1 re-execuções no mesmo detection_period,
    /// cada oportunidade estagnada gera no máximo 1 OpportunityStale.
    /// Verifica que RegisterAsync é chamado exatamente 1 vez independente de N re-execuções.
    /// FsCheck gera N de 1 a 10 re-execuções aleatoriamente (MaxTest=100).
    /// </summary>
    [Property(MaxTest = 100, DisplayName = "PBT-09: StagnationDetectionService idempotente por (opportunity_id, detection_period)")]
    public Property PBT09_N_reexecucoes_no_mesmo_periodo_geram_no_maximo_1_OpportunityStale()
    {
        var nGen = Gen.Choose(2, 10); // N re-execuções (mínimo 2 para verificar idempotência)

        return Prop.ForAll(
            Arb.From(nGen),
            n =>
            {
                // Arrange: uma oportunidade estagnada (15 dias de inatividade)
                var opportunityId = Guid.NewGuid();
                var tenantId = Guid.NewGuid();
                var buId = Guid.NewGuid();
                var actorId = Guid.NewGuid();
                var now = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
                var staleThreshold = now.AddDays(-15);
                var detectionPeriod = "2026-06";

                var repoLocal = Substitute.For<IOpportunityRepository>();
                var queryRepoLocal = Substitute.For<IOpportunityQueryRepository>();
                var activityPortLocal = Substitute.For<IActivityReadPort>();
                var detectionRunRepoLocal = Substitute.For<IStaleDetectionRunRepository>();
                var uowLocal = Substitute.For<IUnitOfWork>();
                var clockLocal = Substitute.For<IClock>();
                clockLocal.UtcNow.Returns(now);
                clockLocal.Today.Returns(DateOnly.FromDateTime(now.DateTime));

                queryRepoLocal.ListOpenOpportunityIdsAsync(tenantId, buId, Arg.Any<CancellationToken>())
                    .Returns(new List<Guid> { opportunityId }.AsReadOnly());

                var opp = BuildOpenOpportunity(tenantId, buId, actorId);
                repoLocal.GetByIdAsync(opportunityId, tenantId, Arg.Any<CancellationToken>()).Returns(opp);
                activityPortLocal.GetLastActivityAtAsync(tenantId, opportunityId, Arg.Any<CancellationToken>())
                    .Returns(staleThreshold);
                uowLocal.PendingDomainEvents.Returns(new List<DomainEvent>().AsReadOnly());

                // Simula idempotência: após primeira execução, ExistsAsync retorna true
                var hasRunCount = 0;
                detectionRunRepoLocal.ExistsAsync(tenantId, opportunityId, detectionPeriod, Arg.Any<CancellationToken>())
                    .Returns(ci => Task.FromResult(hasRunCount > 0));
                detectionRunRepoLocal
                    .When(r => r.RegisterAsync(tenantId, opportunityId, detectionPeriod, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()))
                    .Do(_ => hasRunCount++);

                var service = new StagnationDetectionService(
                    repoLocal, queryRepoLocal, activityPortLocal, detectionRunRepoLocal,
                    uowLocal, clockLocal, _logger);

                // Act: N execuções no mesmo período
                for (int i = 0; i < n; i++)
                {
                    service.DetectAsync(tenantId, buId, CancellationToken.None).GetAwaiter().GetResult();
                }

                // Assert: RegisterAsync chamado exatamente 1 vez (idempotência)
                detectionRunRepoLocal.Received(1)
                    .RegisterAsync(tenantId, opportunityId, detectionPeriod, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                    .GetAwaiter().GetResult();

                // MarkStale idempotente no agregado: apenas 1 domainEvent emitido
                // (verificado via IsStale=true após primeira execução → MarkStale é no-op nas seguintes)
                opp.IsStale.Should().BeTrue("oportunidade deve ter sido marcada como estagnada na 1ª execução");

                return true;
            });
    }

    // =========================================================================
    // Testes unitários determinísticos
    // =========================================================================

    [Fact]
    public async Task Oportunidade_sem_atividade_ha_mais_de_14_dias_eh_marcada_como_estagnada()
    {
        // Arrange: oportunidade com last_activity_at há 20 dias
        var opportunityId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        var lastActivity = now.AddDays(-20); // 20 dias > 14 threshold

        _clock.UtcNow.Returns(now);
        _clock.Today.Returns(DateOnly.FromDateTime(now.DateTime));
        _queryRepo.ListOpenOpportunityIdsAsync(_tenantId, _buId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { opportunityId }.AsReadOnly());

        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        _repo.GetByIdAsync(opportunityId, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _activityPort.GetLastActivityAtAsync(_tenantId, opportunityId, Arg.Any<CancellationToken>())
            .Returns(lastActivity);
        _detectionRunRepo.ExistsAsync(_tenantId, opportunityId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _uow.PendingDomainEvents.Returns(new List<DomainEvent>().AsReadOnly());

        var service = BuildService();

        // Act
        await service.DetectAsync(_tenantId, _buId, CancellationToken.None);

        // Assert
        opp.IsStale.Should().BeTrue();
        await _repo.Received(1).SaveAsync(opp, Arg.Any<CancellationToken>());
        await _detectionRunRepo.Received(1).RegisterAsync(
            _tenantId, opportunityId, "2026-06", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Oportunidade_com_atividade_recente_nao_eh_marcada_como_estagnada()
    {
        // Arrange: last_activity_at há 5 dias (< 14 threshold)
        var opportunityId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        var lastActivity = now.AddDays(-5);

        _clock.UtcNow.Returns(now);
        _clock.Today.Returns(DateOnly.FromDateTime(now.DateTime));
        _queryRepo.ListOpenOpportunityIdsAsync(_tenantId, _buId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { opportunityId }.AsReadOnly());

        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        _repo.GetByIdAsync(opportunityId, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _activityPort.GetLastActivityAtAsync(_tenantId, opportunityId, Arg.Any<CancellationToken>())
            .Returns(lastActivity);
        _detectionRunRepo.ExistsAsync(_tenantId, opportunityId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var service = BuildService();

        // Act
        await service.DetectAsync(_tenantId, _buId, CancellationToken.None);

        // Assert
        opp.IsStale.Should().BeFalse();
        await _repo.DidNotReceive().SaveAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Oportunidade_ja_processada_no_periodo_e_ignorada_idempotencia()
    {
        // Arrange: ExistsAsync retorna true (já processado no período)
        var opportunityId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _clock.UtcNow.Returns(now);
        _clock.Today.Returns(DateOnly.FromDateTime(now.DateTime));
        _queryRepo.ListOpenOpportunityIdsAsync(_tenantId, _buId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { opportunityId }.AsReadOnly());

        _detectionRunRepo.ExistsAsync(_tenantId, opportunityId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true); // já processado

        var service = BuildService();

        // Act
        await service.DetectAsync(_tenantId, _buId, CancellationToken.None);

        // Assert: não consulta activity port nem repositório
        await _activityPort.DidNotReceive()
            .GetLastActivityAtAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activity_port_indisponivel_nao_marca_oportunidade_como_estagnada()
    {
        // Degradação graciosa: null de IActivityReadPort → não marca
        var opportunityId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _clock.UtcNow.Returns(now);
        _clock.Today.Returns(DateOnly.FromDateTime(now.DateTime));
        _queryRepo.ListOpenOpportunityIdsAsync(_tenantId, _buId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { opportunityId }.AsReadOnly());
        _detectionRunRepo.ExistsAsync(_tenantId, opportunityId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _activityPort.GetLastActivityAtAsync(_tenantId, opportunityId, Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null); // serviço indisponível

        var service = BuildService();

        // Act
        await service.DetectAsync(_tenantId, _buId, CancellationToken.None);

        // Assert: não marca, não salva
        await _repo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().SaveAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Oportunidade_ja_estagnada_nao_emite_segundo_OpportunityStale()
    {
        // MarkStale é idempotente no agregado: IsStale=true → não emite segundo evento
        var opportunityId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        var lastActivity = now.AddDays(-20);

        _clock.UtcNow.Returns(now);
        _clock.Today.Returns(DateOnly.FromDateTime(now.DateTime));
        _queryRepo.ListOpenOpportunityIdsAsync(_tenantId, _buId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { opportunityId }.AsReadOnly());

        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);
        // Marca como estagnada diretamente no agregado (simula estado persistido)
        opp.MarkStale(lastActivity, now, DateOnly.FromDateTime(now.DateTime));
        opp.ClearDomainEvents();

        _repo.GetByIdAsync(opportunityId, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _activityPort.GetLastActivityAtAsync(_tenantId, opportunityId, Arg.Any<CancellationToken>())
            .Returns(lastActivity);
        _detectionRunRepo.ExistsAsync(_tenantId, opportunityId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false); // período diferente → processa, mas IsStale já é true

        var service = BuildService();

        // Act
        await service.DetectAsync(_tenantId, _buId, CancellationToken.None);

        // Assert: IsStale já era true → MarkStale é idempotente → não salva novamente
        await _repo.DidNotReceive().SaveAsync(Arg.Any<Opportunity>(), Arg.Any<CancellationToken>());
    }
}
