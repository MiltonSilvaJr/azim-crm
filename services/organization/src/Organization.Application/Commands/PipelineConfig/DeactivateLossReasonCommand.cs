using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Command para desativar um motivo de perda de uma Business Unit.
/// A <c>BusinessUnitEnablementSpec</c> garante que ao menos um motivo ativo seja preservado (ORG-ERR-017).
/// Autorizado para <c>TAdmin</c>.
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
/// <param name="ReasonId">Identificador do motivo a desativar.</param>
[RequiresRole("TAdmin")]
public sealed record DeactivateLossReasonCommand(Guid BuId, Guid ReasonId) : ICommand;

/// <summary>Validador sintático do <see cref="DeactivateLossReasonCommand"/>.</summary>
public sealed class DeactivateLossReasonCommandValidator : AbstractValidator<DeactivateLossReasonCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public DeactivateLossReasonCommandValidator()
    {
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.ReasonId).NotEmpty();
    }
}
