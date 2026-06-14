using MediatR;
using Reporting.Application.Ports;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;

namespace Reporting.Application.Queries.Channel;

/// <summary>
/// Handler do relatório de oportunidades por canal (Req 3).
///
/// Responsabilidades:
/// <list type="number">
///   <item><description>Chama <see cref="IReportingReadRepository.GetChannelAsync"/> com escopo e período.</description></item>
///   <item><description>Calcula <c>PercentBasisPoints</c> em aritmética inteira (base 10.000) para conservar soma = 100% sem float (DD-010, PBT-04).</description></item>
///   <item><description>Canal com zero oportunidades recebe <c>PercentBasisPoints = 0</c> (sem exclusão).</description></item>
///   <item><description>Retorna as linhas com PercentBasisPoints calculados.</description></item>
/// </list>
///
/// O repositório retorna contagens brutas sem percentual — o cálculo é responsabilidade do handler.
///
/// Mapeia: TASK-09, design §5.3, Req 3, DD-010, PBT-04.
/// </summary>
public sealed class GetChannelReportQueryHandler : IRequestHandler<GetChannelReportQuery, ChannelReportResponse>
{
    private readonly IReportingReadRepository _repository;

    /// <summary>Inicializa o handler com o repositório de leitura.</summary>
    public GetChannelReportQueryHandler(IReportingReadRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<ChannelReportResponse> Handle(
        GetChannelReportQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawRows = await _repository.GetChannelAsync(
            request.Scope,
            request.Period,
            request.BuIds,
            cancellationToken);

        var rows = CalculateBasisPoints(rawRows);
        return new ChannelReportResponse(rows);
    }

    /// <summary>
    /// Calcula <c>PercentBasisPoints</c> para cada canal usando aritmética inteira.
    /// Conserva soma = 10.000 sem float (PBT-04, DD-010).
    /// </summary>
    public static IReadOnlyList<ChannelRow> CalculateBasisPoints(IReadOnlyList<ChannelRow> rawRows)
    {
        if (rawRows.Count == 0)
        {
            return [];
        }

        long totalCount = rawRows.Sum(r => (long)r.Count);

        if (totalCount == 0)
        {
            // Todos os canais com zero oportunidades: todos com 0 basis points
            return rawRows.Select(r => r with { PercentBasisPoints = 0 }).ToList();
        }

        // Algoritmo de distribuição "largest remainder" para garantir soma exata = 10.000
        // sem usar float/double (DD-010, PBT-04).
        var exactValues  = rawRows.Select(r => (r, exact: (long)r.Count * 10_000L)).ToArray();
        var floors       = exactValues.Select(x => (int)(x.exact / totalCount)).ToArray();
        var sumOfFloors  = floors.Sum();
        var remainder    = 10_000 - sumOfFloors;

        // Distribui o restante para os canais com maior parte fracionária
        var fractionals  = exactValues
            .Select((x, i) => (index: i, frac: x.exact % totalCount))
            .OrderByDescending(x => x.frac)
            .Take(remainder)
            .Select(x => x.index)
            .ToHashSet();

        var result = new List<ChannelRow>(rawRows.Count);
        for (var i = 0; i < rawRows.Count; i++)
        {
            var bp = floors[i] + (fractionals.Contains(i) ? 1 : 0);
            result.Add(rawRows[i] with { PercentBasisPoints = bp });
        }

        return result;
    }
}
