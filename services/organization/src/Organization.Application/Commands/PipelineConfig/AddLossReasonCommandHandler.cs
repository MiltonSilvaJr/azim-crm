using MediatR;
using Organization.Application.Ports;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Handler para <see cref="AddLossReasonCommand"/>.
/// </summary>
public sealed class AddLossReasonCommandHandler : IRequestHandler<AddLossReasonCommand>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public AddLossReasonCommandHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(AddLossReasonCommand request, CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        bu.AddLossReason(request.Name, request.ReasonId);

        await _buRepository.SaveAsync(bu, cancellationToken);
    }
}
