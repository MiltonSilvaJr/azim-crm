using FluentValidation;
using Organization.Application.Abstractions;
using Organization.Application.Behaviors;

namespace Organization.Application.Commands.Invitation;

/// <summary>
/// Command para aceitar um convite via token.
/// Idempotente: reprocessar o mesmo token não cria duplicatas (PBT-03, Req 4.4).
/// Não requer JWT — autenticado pelo token de convite (§8).
/// </summary>
/// <param name="PlainToken">Token de convite em claro (PII — não logar).</param>
/// <param name="DisplayName">Nome de exibição do usuário (PII — não logar).</param>
[RequiresRole(AllowAnonymous = true)]
public sealed record AcceptInvitationCommand(string PlainToken, string DisplayName)
    : ICommand<Guid>, IIdempotentRequest
{
    /// <summary>Chave de idempotência: hash do token computado no handler.</summary>
    public string IdempotencyKey => PlainToken; // O behavior vai resolver o hash via ITokenHasher

    /// <summary>Tipo do evento para rastreabilidade no Inbox.</summary>
    public string EventType => "invitation.accept";
}

/// <summary>Validador sintático do <see cref="AcceptInvitationCommand"/>.</summary>
public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.PlainToken)
            .NotEmpty().WithMessage("O token de convite é obrigatório.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("O nome de exibição é obrigatório.")
            .MaximumLength(200).WithMessage("O nome de exibição não pode exceder 200 caracteres.");
    }
}
