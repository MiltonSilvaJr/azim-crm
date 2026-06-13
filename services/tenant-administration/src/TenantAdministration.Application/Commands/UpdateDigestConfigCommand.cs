using FluentValidation;
using MediatR;
using TenantAdministration.Application.Authorization;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Resultado da atualização de configuração de digest.
/// </summary>
/// <param name="TenantId">Identificador do tenant atualizado.</param>
/// <param name="Timezone">Novo fuso horário IANA.</param>
/// <param name="DigestTime">Novo horário do digest no formato HH:mm.</param>
public sealed record UpdateDigestConfigResult(Guid TenantId, string Timezone, string DigestTime);

/// <summary>
/// Command para atualizar fuso horário e horário do digest de um tenant.
/// Restrito ao Tenant Admin.
/// ATENÇÃO: <c>Slug</c> no corpo é explicitamente proibido (TA-ERR-011 — Req 2.3).
/// design.md §5.1 e §8.2 PATCH /api/v1/tenant.
/// </summary>
[RequiresTenantAdmin]
public sealed record UpdateDigestConfigCommand(
    Guid TenantId,
    string Timezone,
    string DigestTime,
    string? Slug = null) : IRequest<UpdateDigestConfigResult>;

/// <summary>
/// Validador de <see cref="UpdateDigestConfigCommand"/>.
/// Rejeita <c>Slug</c> no corpo (TA-ERR-011) e valida campos obrigatórios.
/// </summary>
public sealed class UpdateDigestConfigCommandValidator : AbstractValidator<UpdateDigestConfigCommand>
{
    public UpdateDigestConfigCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEqual(Guid.Empty)
            .WithMessage("TenantId é obrigatório.");

        // TA-ERR-011: slug imutável — não pode aparecer no corpo do PATCH
        RuleFor(x => x.Slug)
            .Null()
            .WithErrorCode("TA-ERR-011")
            .WithMessage("Slug não pode ser alterado. O slug é imutável (Req 2.3).");

        RuleFor(x => x.Timezone)
            .NotEmpty()
            .WithErrorCode("TA-ERR-001")
            .WithMessage("Timezone é obrigatório.");

        RuleFor(x => x.DigestTime)
            .NotEmpty()
            .WithErrorCode("TA-ERR-001")
            .WithMessage("DigestTime é obrigatório.");
    }
}
