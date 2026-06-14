using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.Membership;

/// <summary>
/// Command para atribuir um membership (BU-papel) a um usuário.
/// Respeita <c>MembershipUniquenessSpec</c> — duplicata retorna ORG-ERR-012.
/// Autorizado para <c>TAdmin</c>.
/// </summary>
/// <param name="UserId">Identificador do usuário.</param>
/// <param name="BuId">Identificador da BU.</param>
/// <param name="Role">Papel a atribuir.</param>
[RequiresRole("TAdmin")]
public sealed record AssignMembershipCommand(Guid UserId, Guid BuId, string Role) : ICommand;

/// <summary>Validador sintático do <see cref="AssignMembershipCommand"/>.</summary>
public sealed class AssignMembershipCommandValidator : AbstractValidator<AssignMembershipCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public AssignMembershipCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => new[] { "TAdmin", "GestorBU", "Vendedor", "Viewer" }.Contains(r))
            .WithMessage("Papel inválido para o vínculo. ORG-ERR-007");
    }
}
