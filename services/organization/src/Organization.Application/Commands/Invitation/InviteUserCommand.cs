using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.Invitation;

/// <summary>
/// Command para convidar um usuário por e-mail para o tenant.
/// Cria convite <c>Pending</c> e enfileira envio de e-mail via Outbox (DD-004).
/// Autorizado apenas para <c>TAdmin</c>.
/// </summary>
/// <param name="Email">E-mail do convidado (PII).</param>
/// <param name="TargetMemberships">Memberships a criar no aceite (BU → papel).</param>
/// <param name="ExpiresAt">Data de expiração do convite (≤ 72h, RN-030).</param>
[RequiresRole("TAdmin")]
public sealed record InviteUserCommand(
    string Email,
    IReadOnlyList<(Guid BuId, string Role)> TargetMemberships,
    DateTimeOffset ExpiresAt) : ICommand<Guid>;

/// <summary>Validador sintático do <see cref="InviteUserCommand"/>.</summary>
public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail do convidado é obrigatório.")
            .EmailAddress().WithMessage("O e-mail do convidado é inválido.");

        RuleFor(x => x.TargetMemberships)
            .NotEmpty().WithMessage("O convite deve ter ao menos um vínculo BU/papel.");

        RuleForEach(x => x.TargetMemberships).ChildRules(m =>
        {
            m.RuleFor(t => t.BuId)
                .NotEmpty().WithMessage("O identificador da BU é obrigatório.");
            m.RuleFor(t => t.Role)
                .NotEmpty().WithMessage("O papel (role) é obrigatório.")
                .Must(r => new[] { "TAdmin", "GestorBU", "Vendedor", "Viewer" }.Contains(r))
                .WithMessage("Papel inválido para o vínculo. ORG-ERR-007");
        });
    }
}
