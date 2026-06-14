using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Command para desativar um canal de origem de uma Business Unit.
/// Autorizado para <c>TAdmin</c>.
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
/// <param name="ChannelId">Identificador do canal a desativar.</param>
[RequiresRole("TAdmin")]
public sealed record DeactivateOriginChannelCommand(Guid BuId, Guid ChannelId) : ICommand;

/// <summary>Validador sintático do <see cref="DeactivateOriginChannelCommand"/>.</summary>
public sealed class DeactivateOriginChannelCommandValidator : AbstractValidator<DeactivateOriginChannelCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public DeactivateOriginChannelCommandValidator()
    {
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.ChannelId).NotEmpty();
    }
}
