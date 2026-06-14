using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.Membership;

/// <summary>
/// Command para alterar o papel de um membership existente.
/// Guarda <c>LastTenantAdminPolicy</c> ao rebaixar TAdmin (ORG-ERR-009).
/// Autorizado para <c>TAdmin</c>.
/// </summary>
/// <param name="UserId">Identificador do usuário.</param>
/// <param name="BuId">Identificador da BU.</param>
/// <param name="NewRole">Novo papel.</param>
[RequiresRole("TAdmin")]
public sealed record ChangeMembershipRoleCommand(Guid UserId, Guid BuId, string NewRole) : ICommand;

/// <summary>Validador sintático do <see cref="ChangeMembershipRoleCommand"/>.</summary>
public sealed class ChangeMembershipRoleCommandValidator : AbstractValidator<ChangeMembershipRoleCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public ChangeMembershipRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.NewRole)
            .NotEmpty()
            .Must(r => new[] { "TAdmin", "GestorBU", "Vendedor", "Viewer" }.Contains(r))
            .WithMessage("Papel inválido para o vínculo. ORG-ERR-007");
    }
}
