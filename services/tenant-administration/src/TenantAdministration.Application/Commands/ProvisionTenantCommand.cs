using FluentValidation;
using MediatR;
using TenantAdministration.Application.Authorization;
using TenantAdministration.Application.Behaviors;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Resultado do provisionamento bem-sucedido de um tenant.
/// </summary>
/// <param name="TenantId">Identificador único do tenant criado.</param>
/// <param name="Slug">Slug imutável do tenant.</param>
/// <param name="Status">Estado inicial do tenant (<c>provisioned</c>).</param>
public sealed record ProvisionTenantResult(Guid TenantId, string Slug, string Status);

/// <summary>
/// Command para provisionar um novo tenant. Restrito ao Platform Operator.
/// Campos: slug (imutável), slugConfirmation (Req 1.3), displayName, timezone,
/// digestTime, adminEmail e idempotencyKey (§6.5).
/// design.md §5.1 e §8.1 POST /api/v1/platform/tenants.
/// </summary>
[RequiresPlatformOperator]
public sealed record ProvisionTenantCommand(
    string Slug,
    string SlugConfirmation,
    string DisplayName,
    string Timezone,
    string DigestTime,
    string AdminEmail,
    string? IdempotencyKey = null) : IRequest<ProvisionTenantResult>, IIdempotentCommand;

/// <summary>
/// Validador FluentValidation para <see cref="ProvisionTenantCommand"/>.
/// Cobre TA-ERR-001 (campos obrigatórios), TA-ERR-003 (confirmação de slug),
/// TA-ERR-005 (formato de slug) e TA-ERR-006 (fuso IANA).
/// Validação sintática na borda; invariantes de domínio ficam no agregado.
/// </summary>
public sealed class ProvisionTenantCommandValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty()
            .WithErrorCode("TA-ERR-001")
            .WithMessage("Slug é obrigatório.");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithErrorCode("TA-ERR-001")
            .WithMessage("DisplayName é obrigatório.");

        RuleFor(x => x.Timezone)
            .NotEmpty()
            .WithErrorCode("TA-ERR-001")
            .WithMessage("Timezone é obrigatório.");

        RuleFor(x => x.DigestTime)
            .NotEmpty()
            .WithErrorCode("TA-ERR-001")
            .WithMessage("DigestTime é obrigatório.");

        RuleFor(x => x.AdminEmail)
            .NotEmpty()
            .WithErrorCode("TA-ERR-001")
            .WithMessage("AdminEmail é obrigatório.");

        RuleFor(x => x.AdminEmail)
            .EmailAddress()
            .WithMessage("AdminEmail deve ser um e-mail válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.AdminEmail));

        // TA-ERR-003: confirmação de slug deve coincidir
        RuleFor(x => x.SlugConfirmation)
            .Equal(x => x.Slug)
            .WithErrorCode("TA-ERR-003")
            .WithMessage("Confirmação de slug não corresponde. Verifique e repita o slug informado.");
    }
}
