using MediatR;
using Organization.Application.Ports;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Handler para <see cref="DeactivateOriginChannelCommand"/>.
/// </summary>
public sealed class DeactivateOriginChannelCommandHandler : IRequestHandler<DeactivateOriginChannelCommand>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public DeactivateOriginChannelCommandHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(DeactivateOriginChannelCommand request, CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        bu.DeactivateOriginChannel(request.ChannelId);

        await _buRepository.SaveAsync(bu, cancellationToken);
    }
}
