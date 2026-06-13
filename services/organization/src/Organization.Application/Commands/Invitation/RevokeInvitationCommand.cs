using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.Invitation;

/// <summary>
/// Command para revogar um convite em estado <c>Pending</c>.
/// Transição: <c>Pending → Revoked</c>.
/// Autorizado apenas para <c>TAdmin</c>.
/// </summary>
/// <param name="InvitationId">Identificador do convite a revogar.</param>
[RequiresRole("TAdmin")]
public sealed record RevokeInvitationCommand(Guid InvitationId) : ICommand;

/// <summary>Validador sintático do <see cref="RevokeInvitationCommand"/>.</summary>
public sealed class RevokeInvitationCommandValidator : AbstractValidator<RevokeInvitationCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public RevokeInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationId)
            .NotEmpty().WithMessage("O identificador do convite é obrigatório.");
    }
}
