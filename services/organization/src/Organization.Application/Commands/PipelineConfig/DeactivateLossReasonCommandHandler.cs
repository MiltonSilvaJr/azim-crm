using MediatR;
using Organization.Application.Ports;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Handler para <see cref="DeactivateLossReasonCommand"/>.
/// Delega a invariante de último motivo ativo ao agregado <c>BusinessUnit</c> (ORG-ERR-017).
/// </summary>
public sealed class DeactivateLossReasonCommandHandler : IRequestHandler<DeactivateLossReasonCommand>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public DeactivateLossReasonCommandHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(DeactivateLossReasonCommand request, CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        bu.DeactivateLossReason(request.ReasonId);

        await _buRepository.SaveAsync(bu, cancellationToken);
    }
}
