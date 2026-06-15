using FluentAssertions;
using NSubstitute;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Application.Opportunities.Queries;
using OpportunityPipeline.Application.SavedFilters;
using OpportunityPipeline.Domain.Opportunities;
using OpportunityPipeline.Domain.Opportunities.Entities;
using OpportunityPipeline.Domain.Opportunities.Ports;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;
using Xunit;

namespace OpportunityPipeline.Application.Tests.Opportunities.Queries;

/// <summary>
/// Testes dos query handlers de oportunidade.
/// Cobre: GetOpportunity (flags derivadas), ListOpportunities, GetKanban (somas + isolamento tenant),
/// GetTimeline, GetCommission (forecast_liquido), GetForecast, GetStale, ListSavedFilters.
/// Mapeia: design §5.2, TASK-12 ST-02..06.
/// </summary>
public sealed class OpportunityQueriesTests
{
    private readonly IOpportunityQueryRepository _queryRepo = Substitute.For<IOpportunityQueryRepository>();
    private readonly ISavedFilterRepository _filterRepo = Substitute.For<ISavedFilterRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private TenantContext BuildContext(Guid? tenantId = null)
    {
        var ctx = new TenantContext();
        ctx.Initialize(tenantId ?? _tenantId, _buId, _actorId);
        return ctx;
    }

    private static Opportunity BuildOpenOpportunity(
        Guid tenantId,
        Guid buId,
        Guid actorId,
        DateOnly? expectedCloseDate = null)
    {
        var stage = new StageRef(Guid.NewGuid(), "Qualificação", StageCategory.Open, 30, 1);
        var channel = new OriginChannelRef(Guid.NewGuid(), "Inbound", false);
        return Opportunity.Create(
            tenantId, buId, Guid.NewGuid(), actorId, null,
            stage, channel, "Opp Query Test",
            new ContractValue(new Money(100000, "BRL"), new Money(5000, "BRL"), 12),
            new Probability(80), expectedCloseDate, null,
            new OpportunityNumber("AZ-0001"), actorId, DateTimeOffset.UtcNow);
    }

    // =========================================================================
    // GetOpportunityQuery
    // =========================================================================

    [Fact]
    public async Task GetOpportunity_retorna_detalhe_com_flags_derivadas_is_overdue_e_is_stale()
    {
        // is_overdue derivada de OverdueSpecification (não persistida)
        var pastDate = new DateOnly(2026, 1, 1);
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId, expectedCloseDate: pastDate);

        _queryRepo.GetByIdWithDetailsAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _clock.Today.Returns(new DateOnly(2026, 6, 14));
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        var handler = new GetOpportunityHandler(_queryRepo, BuildContext(), _clock);
        var query = new GetOpportunityQuery(opp.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Id.Should().Be(opp.Id);
        result.IsOverdue.Should().BeTrue(); // data no passado com stage open
        result.IsStale.Should().BeFalse(); // não marcada como estagnada
        result.TotalValueCents.Should().Be(opp.ContractValue.TotalInCents);
    }

    [Fact]
    public async Task GetOpportunity_oportunidade_nao_encontrada_lanca_ValidationException()
    {
        var missingId = Guid.NewGuid();
        _queryRepo.GetByIdWithDetailsAsync(missingId, _tenantId, Arg.Any<CancellationToken>())
            .Returns((Opportunity?)null);
        _clock.Today.Returns(DateOnly.FromDateTime(DateTime.UtcNow));

        var handler = new GetOpportunityHandler(_queryRepo, BuildContext(), _clock);
        var query = new GetOpportunityQuery(missingId);

        var act = () => handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetOpportunity_is_overdue_falso_quando_stage_nao_e_open()
    {
        // Oportunidade won com data passada → is_overdue = false (especificação exige Open)
        var pastDate = new DateOnly(2026, 1, 1);
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId, expectedCloseDate: pastDate);
        opp.Win(_actorId, DateTimeOffset.UtcNow);
        opp.ClearDomainEvents();

        _queryRepo.GetByIdWithDetailsAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _clock.Today.Returns(new DateOnly(2026, 6, 14));
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        var handler = new GetOpportunityHandler(_queryRepo, BuildContext(), _clock);
        var result = await handler.Handle(new GetOpportunityQuery(opp.Id), CancellationToken.None);

        result.IsOverdue.Should().BeFalse(); // Won → não overdue mesmo com data passada
        result.StageCategory.Should().Be(StageCategory.Won);
    }

    // =========================================================================
    // GetKanbanQuery — ST-02
    // =========================================================================

    [Fact]
    public async Task GetKanban_retorna_colunas_com_somas_agregadas_por_estagio()
    {
        // ST-02: SUM(valor_total) e SUM(forecast_ponderado) por estágio
        var stageId = Guid.NewGuid();
        var columns = new List<KanbanColumn>
        {
            new KanbanColumn(
                StageId: stageId,
                StageName: "Qualificação",
                Order: 1,
                TotalValueCents: 500000L,
                ForecastPonderadoCents: 250000L,
                TotalCount: 5,
                Cards: new List<OpportunitySummary>())
        };

        _queryRepo.GetKanbanAsync(_tenantId, _buId, 20, Arg.Any<CancellationToken>())
            .Returns(columns.AsReadOnly());

        var handler = new GetKanbanHandler(_queryRepo, BuildContext());
        var result = await handler.Handle(new GetKanbanQuery { StagePageSize = 20 }, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].TotalValueCents.Should().Be(500000L);
        result[0].ForecastPonderadoCents.Should().Be(250000L);
        result[0].TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task GetKanban_isolamento_tenant_nao_retorna_oportunidades_de_outro_tenant()
    {
        // ST-02: TenantContext diferente → repositório é chamado com tenantId correto
        var otherTenantId = Guid.NewGuid();
        _queryRepo.GetKanbanAsync(otherTenantId, _buId, 20, Arg.Any<CancellationToken>())
            .Returns(new List<KanbanColumn>().AsReadOnly()); // tenant diferente → vazio

        var handler = new GetKanbanHandler(_queryRepo, BuildContext(otherTenantId));
        var result = await handler.Handle(new GetKanbanQuery { StagePageSize = 20 }, CancellationToken.None);

        // Verifica que foi chamado com o tenant correto (isolamento por design)
        result.Should().BeEmpty();
        await _queryRepo.Received(1).GetKanbanAsync(otherTenantId, _buId, 20, Arg.Any<CancellationToken>());
        await _queryRepo.DidNotReceive().GetKanbanAsync(_tenantId, _buId, 20, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // GetCommissionQuery — inclui forecast_liquido
    // =========================================================================

    [Fact]
    public async Task GetCommission_sem_comissao_retorna_forecast_liquido_igual_ao_forecast_ponderado()
    {
        // Sem comissão: forecast_liquido = forecast_ponderado (NetForecastCalculator sem comissão)
        var opp = BuildOpenOpportunity(_tenantId, _buId, _actorId);

        _queryRepo.GetByIdWithDetailsAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _queryRepo.GetCommissionsAsync(_tenantId, opp.Id, Arg.Any<CancellationToken>())
            .Returns(new List<CommissionDetail>().AsReadOnly());

        var handler = new GetCommissionHandler(_queryRepo, BuildContext());
        var result = await handler.Handle(new GetCommissionQuery(opp.Id), CancellationToken.None);

        // forecast_liquido = forecast_ponderado quando sem comissão
        // ContractValue(100000 setup, 5000*12=60000 mensal) = 160000; prob=80 → forecast=128000
        var expectedForecast = 128000L; // round(160000 * 80 / 100)
        result.ForecastLiquidoCents.Should().Be(expectedForecast);
        result.Commissions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommission_com_comissao_retorna_forecast_liquido_deduzido()
    {
        // Com comissão 10% setup: comissao_total = 10000; forecast_liquido = 128000 - round(10000*80/100)
        var partnerId = Guid.NewGuid();
        var stage = new StageRef(Guid.NewGuid(), "Q", StageCategory.Open, 30, 1);
        var partnerChannel = new OriginChannelRef(Guid.NewGuid(), "Parceiro", true);
        var opp = Opportunity.Create(
            _tenantId, _buId, Guid.NewGuid(), _actorId, partnerId,
            stage, partnerChannel, "Opp Com Comissão",
            new ContractValue(new Money(100000, "BRL"), new Money(0, "BRL"), 0), // total = 100000
            new Probability(80), null, null,
            new OpportunityNumber("AZ-0002"), _actorId, DateTimeOffset.UtcNow);

        var terms = new CommissionTerms(CommissionRole.Revendedor, 10m, 0m, null, 0);
        opp.SetPartnerCommission(partnerId, terms, DateTimeOffset.UtcNow);
        opp.ClearDomainEvents();

        _queryRepo.GetByIdWithDetailsAsync(opp.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(opp);
        _queryRepo.GetCommissionsAsync(_tenantId, opp.Id, Arg.Any<CancellationToken>())
            .Returns(new List<CommissionDetail>
            {
                new CommissionDetail(partnerId, "Revendedor", 10m, 0m, 0L, 0, 10000L, 0L, false, null)
            }.AsReadOnly());

        var handler = new GetCommissionHandler(_queryRepo, BuildContext());
        var result = await handler.Handle(new GetCommissionQuery(opp.Id), CancellationToken.None);

        // forecast_ponderado = round(100000 * 80/100) = 80000
        // comissao_ponderada = round(10000 * 80/100) = 8000
        // forecast_liquido = 80000 - 8000 = 72000
        result.ForecastLiquidoCents.Should().Be(72000L);
    }

    // =========================================================================
    // ListOpportunitiesQuery
    // =========================================================================

    [Fact]
    public async Task ListOpportunities_delega_ao_repositorio_com_filtros_e_paginacao()
    {
        var filter = new OpportunityFilter(StageCategory: StageCategory.Open);
        var page = new PageRequest(Page: 2, PageSize: 10);
        var expectedResult = new PagedResult<OpportunitySummary>(
            new List<OpportunitySummary>(), 0, 2, 10);

        _queryRepo.ListAsync(_tenantId, _buId, filter, page, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var handler = new ListOpportunitiesHandler(_queryRepo, BuildContext());
        var result = await handler.Handle(new ListOpportunitiesQuery { Filter = filter, Page = page }, CancellationToken.None);

        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
    }

    // =========================================================================
    // GetForecastQuery
    // =========================================================================

    [Fact]
    public async Task GetForecast_retorna_somas_agregadas_do_repositorio()
    {
        _queryRepo.GetForecastAsync(_tenantId, _buId, Arg.Any<CancellationToken>())
            .Returns((TotalValueCents: 1000000L, ForecastPonderadoCents: 600000L, ForecastLiquidoCents: 540000L));

        var handler = new GetForecastHandler(_queryRepo, BuildContext());
        var result = await handler.Handle(new GetForecastQuery(), CancellationToken.None);

        result.TotalValueCents.Should().Be(1000000L);
        result.ForecastPonderadoCents.Should().Be(600000L);
        result.ForecastLiquidoCents.Should().Be(540000L);
    }

    // =========================================================================
    // ListSavedFiltersQuery
    // =========================================================================

    [Fact]
    public async Task ListSavedFilters_retorna_filtros_do_usuario_no_tenant()
    {
        var filters = new List<SavedFilter>
        {
            new SavedFilter(Guid.NewGuid(), _tenantId, _actorId, "Meu Filtro", """{"stage":"Open"}""", DateTimeOffset.UtcNow)
        };

        _filterRepo.ListByUserAsync(_tenantId, _actorId, Arg.Any<CancellationToken>())
            .Returns(filters.AsReadOnly());

        var handler = new ListSavedFiltersHandler(_filterRepo, BuildContext());
        var result = await handler.Handle(new ListSavedFiltersQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Meu Filtro");
    }

    // =========================================================================
    // GetTimelineQuery
    // =========================================================================

    [Fact]
    public async Task GetTimeline_retorna_transicoes_da_oportunidade()
    {
        var opportunityId = Guid.NewGuid();
        var entries = new List<TimelineEntry>
        {
            new TimelineEntry(Guid.NewGuid(), null, "Qualificação", "None", "Open", DateTimeOffset.UtcNow, _actorId),
            new TimelineEntry(Guid.NewGuid(), "Qualificação", "Proposta", "Open", "Open", DateTimeOffset.UtcNow, _actorId)
        };

        _queryRepo.GetTimelineAsync(_tenantId, opportunityId, Arg.Any<CancellationToken>())
            .Returns(entries.AsReadOnly());

        var handler = new GetTimelineHandler(_queryRepo, BuildContext());
        var result = await handler.Handle(new GetTimelineQuery(opportunityId), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    // =========================================================================
    // GetStaleQuery
    // =========================================================================

    [Fact]
    public async Task GetStale_retorna_oportunidades_estagnadas_paginadas()
    {
        var page = new PageRequest(Page: 1, PageSize: 10);
        var expectedResult = new PagedResult<OpportunitySummary>(
            new List<OpportunitySummary>(), 0, 1, 10);

        _queryRepo.ListStaleAsync(_tenantId, _buId, page, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var handler = new GetStaleHandler(_queryRepo, BuildContext());
        var result = await handler.Handle(new GetStaleQuery { Page = page }, CancellationToken.None);

        result.TotalCount.Should().Be(0);
    }
}
