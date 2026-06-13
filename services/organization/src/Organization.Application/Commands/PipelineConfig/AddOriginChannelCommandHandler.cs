using MediatR;
using Organization.Application.Ports;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Handler para <see cref="AddOriginChannelCommand"/>.
/// </summary>
public sealed class AddOriginChannelCommandHandler : IRequestHandler<AddOriginChannelCommand>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public AddOriginChannelCommandHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(AddOriginChannelCommand request, CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        bu.AddOriginChannel(request.Name, request.ChannelId);

        await _buRepository.SaveAsync(bu, cancellationToken);
    }
}
