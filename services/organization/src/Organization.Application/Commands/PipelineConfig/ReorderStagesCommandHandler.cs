using MediatR;
using Organization.Application.Ports;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Handler para <see cref="ReorderStagesCommand"/>.
/// Delega a validação de unicidade das novas posições ao agregado <c>BusinessUnit</c>.
/// </summary>
public sealed class ReorderStagesCommandHandler : IRequestHandler<ReorderStagesCommand>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public ReorderStagesCommandHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(ReorderStagesCommand request, CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        bu.ReorderStages(request.NewPositions);

        await _buRepository.SaveAsync(bu, cancellationToken);
    }
}
