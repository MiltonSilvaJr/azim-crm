using FluentValidation;
using MediatR;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using AppValidationException = OpportunityPipeline.Application.Common.ValidationException;

namespace OpportunityPipeline.Application.SavedFilters;

// =========================================================================
// SavedFilter — porta de repositório (Application)
// =========================================================================

/// <summary>
/// Representa um filtro salvo pelo usuário.
/// Mapeia: Req 19, design §5.3, TASK-11.
/// </summary>
public sealed record SavedFilter(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Name,
    string CriteriaJson,
    DateTimeOffset CreatedAt);

/// <summary>
/// Porta de repositório para filtros salvos.
/// Implementação na Infrastructure (Onda 4).
/// Mapeia: design §5.3, TASK-11.
/// </summary>
public interface ISavedFilterRepository
{
    /// <summary>Verifica se já existe filtro com o mesmo nome para o usuário no tenant.</summary>
    Task<bool> ExistsByNameAsync(Guid tenantId, Guid userId, string name, CancellationToken cancellationToken = default);

    /// <summary>Persiste novo filtro salvo.</summary>
    Task AddAsync(SavedFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Lista filtros salvos do usuário no tenant.</summary>
    Task<IReadOnlyList<SavedFilter>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

// =========================================================================
// SaveFilterCommand
// =========================================================================

/// <summary>
/// Command para salvar um filtro de busca personalizado.
/// Nome deve ser único por usuário/tenant (OP-ERR padrão 422).
/// Persiste criteria como JSONB no repositório.
/// Mapeia: Req 19, design §5.1, TASK-11 ST-05.
/// </summary>
[RequiresRole(UserRole.Vendedor, UserRole.GestorBU, UserRole.TenantAdmin)]
public sealed record SaveFilterCommand : IRequest<SaveFilterResult>, IAuthenticatedCommand, IMutableAuthenticatedCommand
{
    private UserRole _userRole;
    public UserRole UserRole => _userRole;
    public void SetRole(UserRole role) => _userRole = role;
    public required string CorrelationId { get; init; }

    public required string Name { get; init; }

    /// <summary>Critérios de busca serializados como JSON.</summary>
    public required string CriteriaJson { get; init; }
}

/// <summary>Resultado do salvamento de filtro.</summary>
public sealed record SaveFilterResult(Guid FilterId, string Name);

/// <summary>Validator para SaveFilterCommand.</summary>
public sealed class SaveFilterValidator : AbstractValidator<SaveFilterCommand>
{
    public SaveFilterValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("name é obrigatório.")
            .MaximumLength(100).WithMessage("name não pode exceder 100 caracteres.");

        RuleFor(x => x.CriteriaJson)
            .NotEmpty().WithMessage("criteria_json é obrigatório.");
    }
}

/// <summary>
/// Handler de SaveFilterCommand.
/// Verifica unicidade do nome por usuário/tenant antes de persistir.
/// Mapeia: Req 19, design §5.3, TASK-11 ST-05.
/// </summary>
public sealed class SaveFilterHandler(
    ISavedFilterRepository filterRepository,
    TenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SaveFilterCommand, SaveFilterResult>
{
    public async Task<SaveFilterResult> Handle(
        SaveFilterCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;
        var userId = tenantContext.ActorId;
        var now = DateTimeOffset.UtcNow;

        // Verifica unicidade do nome por usuário/tenant
        var exists = await filterRepository.ExistsByNameAsync(tenantId, userId, command.Name, cancellationToken).ConfigureAwait(false);
        if (exists)
            throw new AppValidationException("name", $"Já existe um filtro com o nome '{command.Name}' para este usuário.");

        var filter = new SavedFilter(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            UserId: userId,
            Name: command.Name,
            CriteriaJson: command.CriteriaJson,
            CreatedAt: now);

        await filterRepository.AddAsync(filter, cancellationToken).ConfigureAwait(false);

        unitOfWork.RegisterAudit(filter.Id, "SavedFilter", new { action = "SaveFilter", name = command.Name });

        return new SaveFilterResult(filter.Id, filter.Name);
    }
}
