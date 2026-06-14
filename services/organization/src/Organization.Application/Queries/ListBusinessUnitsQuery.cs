using MediatR;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Queries;

/// <summary>
/// Query para listar as Business Units ativas do tenant (paginada).
/// Acessível para qualquer papel ativo (Req 6.1).
/// </summary>
/// <param name="Page">Número da página (1-based).</param>
/// <param name="PageSize">Tamanho da página.</param>
[RequiresRole("TAdmin", "GestorBU", "Vendedor", "Viewer")]
public sealed record ListBusinessUnitsQuery(int Page, int PageSize) : IQuery<IReadOnlyList<BusinessUnitResult>>;

/// <summary>Projeção de uma Business Unit.</summary>
public sealed record BusinessUnitResult(Guid Id, string Name, bool Active);

/// <summary>Handler para <see cref="ListBusinessUnitsQuery"/>.</summary>
public sealed class ListBusinessUnitsQueryHandler : IRequestHandler<ListBusinessUnitsQuery, IReadOnlyList<BusinessUnitResult>>
{
    private readonly IBusinessUnitRepository _buRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public ListBusinessUnitsQueryHandler(IBusinessUnitRepository buRepository, ITenantContext tenantContext)
    {
        _buRepository = buRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BusinessUnitResult>> Handle(
        ListBusinessUnitsQuery request,
        CancellationToken cancellationToken)
    {
        var bus = await _buRepository.ListActiveAsync(request.Page, request.PageSize, cancellationToken);
        return bus
            .Select(bu => new BusinessUnitResult(bu.Id, bu.Name.Value, bu.Active))
            .ToList()
            .AsReadOnly();
    }
}
