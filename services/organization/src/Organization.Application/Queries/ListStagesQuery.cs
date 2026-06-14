using MediatR;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Queries;

/// <summary>
/// Query para listar os estágios de pipeline de uma BU, ordenados por <c>position</c>.
/// Consumida por serviços externos (opportunity-pipeline).
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
[RequiresRole(AllowAnonymous = true)]
public sealed record ListStagesQuery(Guid BuId) : IQuery<IReadOnlyList<StageResult>>;

/// <summary>Projeção de um estágio de pipeline.</summary>
public sealed record StageResult(Guid Id, string Name, int ProbabilityPercent, string Category, int Position);

/// <summary>Handler para <see cref="ListStagesQuery"/>.</summary>
public sealed class ListStagesQueryHandler : IRequestHandler<ListStagesQuery, IReadOnlyList<StageResult>>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public ListStagesQueryHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StageResult>> Handle(
        ListStagesQuery request,
        CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        return bu.Stages
            .OrderBy(s => s.Position)
            .Select(s => new StageResult(s.Id, s.Name, s.Probability.Value, s.Category.Value, s.Position))
            .ToList()
            .AsReadOnly();
    }
}
