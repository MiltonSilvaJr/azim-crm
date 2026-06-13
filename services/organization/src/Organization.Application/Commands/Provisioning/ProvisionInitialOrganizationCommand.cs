using FluentValidation;
using Organization.Application.Abstractions;

namespace Organization.Application.Commands.Provisioning;

/// <summary>
/// Command para provisionar a organização inicial após receber o evento <c>TenantProvisioned</c>.
/// Idempotente via <c>IInboxStore</c> (deduplicação por <c>MessageId</c>/<c>TenantId</c>).
/// Cria a BU inicial com seeds (8 estágios, 4 canais, 1 motivo de perda) e o primeiro TAdmin.
/// Autorizado sem autenticação (consumer interno de mensageria).
/// </summary>
/// <param name="MessageId">Identificador único da mensagem (deduplicação Inbox).</param>
/// <param name="TenantId">Identificador do tenant a provisionar.</param>
/// <param name="AdminEmail">E-mail do administrador inicial.</param>
/// <param name="AdminDisplayName">Nome de exibição do administrador inicial.</param>
/// <param name="OrganizationName">Nome da organização (usado como nome da BU inicial).</param>
[RequiresRole(AllowAnonymous = true)]
public sealed record ProvisionInitialOrganizationCommand(
    string MessageId,
    Guid TenantId,
    string AdminEmail,
    string AdminDisplayName,
    string OrganizationName) : ICommand;

/// <summary>Validador sintático do <see cref="ProvisionInitialOrganizationCommand"/>.</summary>
public sealed class ProvisionInitialOrganizationCommandValidator
    : AbstractValidator<ProvisionInitialOrganizationCommand>
{
    /// <summary>Inicializa as regras de validação.</summary>
    public ProvisionInitialOrganizationCommandValidator()
    {
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.AdminDisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OrganizationName).NotEmpty().MaximumLength(200);
    }
}
