using MediatR;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Queries;

/// <summary>
/// Query para listar os canais de origem ativos de uma BU.
/// Consumida por serviços externos (Req 10.4).
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
[RequiresRole(AllowAnonymous = true)]
public sealed record ListOriginChannelsQuery(Guid BuId) : IQuery<IReadOnlyList<OriginChannelResult>>;

/// <summary>Projeção de um canal de origem.</summary>
public sealed record OriginChannelResult(Guid Id, string Name);

/// <summary>Handler para <see cref="ListOriginChannelsQuery"/>.</summary>
public sealed class ListOriginChannelsQueryHandler : IRequestHandler<ListOriginChannelsQuery, IReadOnlyList<OriginChannelResult>>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public ListOriginChannelsQueryHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OriginChannelResult>> Handle(
        ListOriginChannelsQuery request,
        CancellationToken cancellationToken)
    {
        var bu = await _buRepository.GetByIdAsync(request.BuId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business Unit '{request.BuId}' não encontrada.");

        return bu.OriginChannels
            .Where(c => c.Active)
            .Select(c => new OriginChannelResult(c.Id, c.Name))
            .ToList()
            .AsReadOnly();
    }
}
