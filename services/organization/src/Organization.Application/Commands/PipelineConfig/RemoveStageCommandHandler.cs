using MediatR;
using Organization.Application.Ports;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Handler para <see cref="RemoveStageCommand"/>.
/// Delega a validação da <c>TerminalStagesPolicy</c> ao agregado <c>BusinessUnit</c>.
/// </summary>
public sealed class RemoveStageCommandHandler : IRequestHandler<RemoveStageCommand>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public RemoveStageCommandHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(RemoveStageCommand request, CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        bu.RemoveStage(request.StageId);

        await _buRepository.SaveAsync(bu, cancellationToken);
    }
}
