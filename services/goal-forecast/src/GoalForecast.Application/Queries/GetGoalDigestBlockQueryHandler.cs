using GoalForecast.Application.Ports;
using MediatR;

namespace GoalForecast.Application.Queries;

/// <summary>
/// Handler do bloco de metas para o Digest worker.
/// Reutiliza a lógica de composição do painel via <see cref="GetForecastPanelQueryHandler"/>
/// delegando internamente, e adapta o resultado para a semântica do bloco Digest.
///
/// Semântica de ausência (Req 9.2, RN-018): sem meta → <c>present=false</c>,
/// sem nenhum campo monetário na resposta.
///
/// Mapeia: Req 9, RN-018, RN-029, design §5.2, §8.6, TASK-14.
/// </summary>
public sealed class GetGoalDigestBlockQueryHandler
    : IRequestHandler<GetGoalDigestBlockQuery, DigestBlockResult>
{
    private readonly IGoalRepository _repository;
    private readonly IPipelineForecastReader _pipelineReader;

    /// <summary>Inicializa com as portas de repositório e pipeline.</summary>
    public GetGoalDigestBlockQueryHandler(
        IGoalRepository repository,
        IPipelineForecastReader pipelineReader)
    {
        _repository = repository;
        _pipelineReader = pipelineReader;
    }

    /// <inheritdoc/>
    public async Task<DigestBlockResult> Handle(
        GetGoalDigestBlockQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = query.Principal.TenantId;

        // Lê meta (pode ser nula — ausência sinalizada com present=false).
        var goal = await _repository.FindByKey(
            tenantId, query.BuId, query.OwnerId,
            query.Year, query.Month, cancellationToken);

        // Sem meta: Digest omite o bloco completamente (Req 9.2, RN-018).
        if (goal is null)
            return DigestBlockResult.Absent();

        // Com meta: lê pipeline com degradação graciosa (DD-007).
        var pipelineQuery = new ForecastViewQuery(
            TenantId: tenantId,
            BuId: query.BuId,
            OwnerId: query.OwnerId,
            Year: query.Year,
            Month: query.Month);

        var pipeline = await _pipelineReader.Read(pipelineQuery, cancellationToken);

        long valorMetaCents = goal.ValorMeta.Cents;

        if (!pipeline.Available)
        {
            // Pipeline indisponível: bloco presente mas sem dados de realizado/gap.
            return DigestBlockResult.WithMeta(
                valorMeta: valorMetaCents,
                realizado: null,
                gap: null,
                pipelineDisponivel: null,
                pipelineUnavailable: true);
        }

        // Pipeline disponível: calcula gap.
        long realizadoCents = pipeline.WonTotalCents;
        long gap = valorMetaCents - realizadoCents;

        return DigestBlockResult.WithMeta(
            valorMeta: valorMetaCents,
            realizado: realizadoCents,
            gap: gap,
            pipelineDisponivel: pipeline.ForecastPonderadoCents,
            pipelineUnavailable: false);
    }
}
