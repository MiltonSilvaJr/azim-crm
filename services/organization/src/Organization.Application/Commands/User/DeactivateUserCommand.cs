using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.User;

/// <summary>
/// Command para desativar um usuário no tenant.
/// Guards: <c>LastTenantAdminPolicy</c> (ORG-ERR-009) e <c>FutureActivitiesSpec</c> (ORG-ERR-011).
/// Invalida cache de RBAC e emite <c>UserDeactivated</c> via Outbox.
/// Autorizado para <c>TAdmin</c>.
/// </summary>
/// <param name="UserId">Identificador do usuário a desativar.</param>
[RequiresRole("TAdmin")]
public sealed record DeactivateUserCommand(Guid UserId) : ICommand;

/// <summary>Validador sintático do <see cref="DeactivateUserCommand"/>.</summary>
public sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public DeactivateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
