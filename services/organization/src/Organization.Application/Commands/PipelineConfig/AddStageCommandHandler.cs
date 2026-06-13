using MediatR;
using Organization.Application.Ports;
using Organization.Domain.ValueObjects;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Handler para <see cref="AddStageCommand"/>.
/// Delega a validação de unicidade de nome e posição ao agregado <c>BusinessUnit</c>.
/// </summary>
public sealed class AddStageCommandHandler : IRequestHandler<AddStageCommand>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public AddStageCommandHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(AddStageCommand request, CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        var probability = Probability.Create(request.ProbabilityPercent);
        var category = StageCategory.Create(request.Category);

        bu.AddStage(request.Name, probability, category, request.Position, request.StageId);

        await _buRepository.SaveAsync(bu, cancellationToken);
    }
}
