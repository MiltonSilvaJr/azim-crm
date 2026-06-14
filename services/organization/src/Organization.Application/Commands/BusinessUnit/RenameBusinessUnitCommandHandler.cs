using MediatR;
using Organization.Application.Ports;
using Organization.Domain.ValueObjects;

namespace Organization.Application.Commands.BusinessUnit;

/// <summary>
/// Handler para <see cref="RenameBusinessUnitCommand"/>.
/// Valida unicidade do novo nome no tenant (ORG-ERR-001).
/// Respeita RBAC: GestorBU apenas na própria BU (verificado no RbacAuthorizationBehavior com ScopedToBu).
/// </summary>
public sealed class RenameBusinessUnitCommandHandler : IRequestHandler<RenameBusinessUnitCommand>
{
    private readonly IBusinessUnitRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    /// <summary>Inicializa o handler com os ports necessários.</summary>
    public RenameBusinessUnitCommandHandler(
        IBusinessUnitRepository repository,
        ITenantContext tenantContext,
        IClock clock)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task Handle(RenameBusinessUnitCommand request, CancellationToken cancellationToken)
    {
        var bu = await _repository.GetByIdAsync(request.BusinessUnitId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Business Unit '{request.BusinessUnitId}' não encontrada. ORG-ERR-008");

        var newName = BusinessUnitName.Create(request.NewName);

        // Valida unicidade (exceto o nome atual da própria BU)
        if (!string.Equals(bu.Name.Value, newName.Value, StringComparison.OrdinalIgnoreCase))
        {
            var nameExists = await _repository.ExistsByNameAsync(newName.Value, cancellationToken);
            if (nameExists)
                throw new InvalidOperationException(
                    $"Já existe uma Business Unit com o nome '{request.NewName}'. ORG-ERR-001");
        }

        bu.Rename(newName);
        await _repository.SaveAsync(bu, cancellationToken);
    }
}
