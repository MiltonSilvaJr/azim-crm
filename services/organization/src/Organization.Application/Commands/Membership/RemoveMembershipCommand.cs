using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.Membership;

/// <summary>
/// Command para remover um membership de um usuário em uma BU.
/// Guarda <c>LastTenantAdminPolicy</c> para evitar tenant sem TAdmin ativo (ORG-ERR-009).
/// Autorizado para <c>TAdmin</c>.
/// </summary>
/// <param name="UserId">Identificador do usuário.</param>
/// <param name="BuId">Identificador da BU.</param>
[RequiresRole("TAdmin")]
public sealed record RemoveMembershipCommand(Guid UserId, Guid BuId) : ICommand;

/// <summary>Validador sintático do <see cref="RemoveMembershipCommand"/>.</summary>
public sealed class RemoveMembershipCommandValidator : AbstractValidator<RemoveMembershipCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public RemoveMembershipCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.BuId).NotEmpty();
    }
}
