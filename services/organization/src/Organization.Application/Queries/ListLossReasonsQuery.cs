using MediatR;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Queries;

/// <summary>
/// Query para listar os motivos de perda ativos de uma BU.
/// Consumida por serviços externos (Req 11.4).
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
[RequiresRole(AllowAnonymous = true)]
public sealed record ListLossReasonsQuery(Guid BuId) : IQuery<IReadOnlyList<LossReasonResult>>;

/// <summary>Projeção de um motivo de perda.</summary>
public sealed record LossReasonResult(Guid Id, string Name);

/// <summary>Handler para <see cref="ListLossReasonsQuery"/>.</summary>
public sealed class ListLossReasonsQueryHandler : IRequestHandler<ListLossReasonsQuery, IReadOnlyList<LossReasonResult>>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public ListLossReasonsQueryHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LossReasonResult>> Handle(
        ListLossReasonsQuery request,
        CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        return bu.LossReasons
            .Where(r => r.Active)
            .Select(r => new LossReasonResult(r.Id, r.Name))
            .ToList()
            .AsReadOnly();
    }
}
