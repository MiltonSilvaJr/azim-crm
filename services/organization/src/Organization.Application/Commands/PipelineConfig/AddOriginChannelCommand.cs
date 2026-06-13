using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.PipelineConfig;

/// <summary>
/// Command para adicionar um canal de origem a uma Business Unit.
/// Autorizado para <c>TAdmin</c>.
/// </summary>
/// <param name="BuId">Identificador da Business Unit.</param>
/// <param name="ChannelId">Identificador pré-gerado do canal.</param>
/// <param name="Name">Nome do canal de origem.</param>
[RequiresRole("TAdmin")]
public sealed record AddOriginChannelCommand(Guid BuId, Guid ChannelId, string Name) : ICommand;

/// <summary>Validador sintático do <see cref="AddOriginChannelCommand"/>.</summary>
public sealed class AddOriginChannelCommandValidator : AbstractValidator<AddOriginChannelCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public AddOriginChannelCommandValidator()
    {
        RuleFor(x => x.BuId).NotEmpty();
        RuleFor(x => x.ChannelId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
