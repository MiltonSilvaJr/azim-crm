using GoalForecast.Application.Ports;
using GoalForecast.Domain.ValueObjects;
using MediatR;

namespace GoalForecast.Application.Queries;

/// <summary>
/// Handler do painel comparativo "Direção". Orquestra 4 passos (design §5.2):
/// <list type="number">
///   <item>Lê a meta do repositório (pode ser nula — degradação graciosa).</item>
///   <item>Lê won_total e forecast_ponderado via porta IPipelineForecastReader.</item>
///   <item>Trata indisponibilidade do pipeline (available=false → pipelineUnavailable=true).</item>
///   <item>Compõe ForecastPanelResult: gap e pct apenas quando valorMeta presente e > 0.</item>
/// </list>
///
/// Propriedade de totalidade: qualquer combinação válida de entrada retorna resultado
/// bem-formado — nunca 404, nunca NaN (PBT-04).
/// Gap = valorMeta − realizado exato em centavos inteiros (PBT-03).
///
/// Mapeia: Req 5, Req 6, Req 8, PBT-03, PBT-04, DD-006, DD-007, design §5.2, TASK-12.
/// </summary>
public sealed class GetForecastPanelQueryHandler
    : IRequestHandler<GetForecastPanelQuery, ForecastPanelResult>
{
    private readonly IGoalRepository _repository;
    private readonly IPipelineForecastReader _pipelineReader;

    /// <summary>Inicializa com portas de repositório e pipeline.</summary>
    public GetForecastPanelQueryHandler(
        IGoalRepository repository,
        IPipelineForecastReader pipelineReader)
    {
        _repository = repository;
        _pipelineReader = pipelineReader;
    }

    /// <inheritdoc/>
    public async Task<ForecastPanelResult> Handle(
        GetForecastPanelQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = query.Principal.TenantId;

        // Passo 1: lê meta (pode ser nula — degradação graciosa, Req 6).
        var goal = await _repository.FindByKey(
            tenantId, query.BuId, query.OwnerId,
            query.Year, query.Month, cancellationToken);

        // Passo 2: lê pipeline (resiliência: em falha retorna unavailable, DD-007).
        var pipelineQuery = new ForecastViewQuery(
            TenantId: tenantId,
            BuId: query.BuId,
            OwnerId: query.OwnerId,
            Year: query.Year,
            Month: query.Month);

        var pipelineResult = await _pipelineReader.Read(pipelineQuery, cancellationToken);

        // Passo 3 e 4: composição do painel.
        return ComposePanel(query, goal, pipelineResult);
    }

    /// <summary>
    /// Compõe o painel combinando meta (pode ser nula) e resultado do pipeline.
    /// Função total: sem 404, sem NaN, sem zero confundível (PBT-04, DD-006, DD-007).
    /// </summary>
    private static ForecastPanelResult ComposePanel(
        GetForecastPanelQuery query,
        Domain.Aggregates.Goal? goal,
        ForecastViewResult pipelineResult)
    {
        var scope = query.OwnerId.HasValue ? "RESPONSAVEL" : "BU";

        // Pipeline indisponível (DD-007): realizado/pipeline = null, pipelineUnavailable = true.
        long? realizadoCents = pipelineResult.Available ? pipelineResult.WonTotalCents : null;
        long? pipelineDisponivel = pipelineResult.Available ? pipelineResult.ForecastPonderadoCents : null;
        bool pipelineUnavailable = !pipelineResult.Available;

        // Sem meta cadastrada (DD-006): valorMeta, gap e pct = null.
        if (goal is null)
        {
            return new ForecastPanelResult(
                Scope: scope,
                BuId: query.BuId,
                OwnerId: query.OwnerId,
                Year: query.Year,
                Month: query.Month,
                ValorMeta: null,
                Realizado: realizadoCents,
                PipelineDisponivel: pipelineDisponivel,
                Gap: null,
                PctAtingimento: null,
                PipelineUnavailable: pipelineUnavailable);
        }

        // Com meta presente: calcula gap e pct quando pipeline disponível.
        long valorMetaCents = goal.ValorMeta.Cents;
        long? gap = null;
        double? pct = null;

        if (pipelineResult.Available)
        {
            // PBT-03: gap = valorMeta − realizado, exato em centavos inteiros.
            gap = valorMetaCents - pipelineResult.WonTotalCents;

            // pct_atingimento só quando valorMeta > 0 (evita divisão por zero, design §5.2).
            if (valorMetaCents > 0)
                pct = (double)pipelineResult.WonTotalCents / valorMetaCents;
        }

        return new ForecastPanelResult(
            Scope: scope,
            BuId: query.BuId,
            OwnerId: query.OwnerId,
            Year: query.Year,
            Month: query.Month,
            ValorMeta: valorMetaCents,
            Realizado: realizadoCents,
            PipelineDisponivel: pipelineDisponivel,
            Gap: gap,
            PctAtingimento: pct,
            PipelineUnavailable: pipelineUnavailable);
    }
}
