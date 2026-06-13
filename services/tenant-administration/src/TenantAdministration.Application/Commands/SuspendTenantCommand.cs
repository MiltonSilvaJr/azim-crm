using FluentValidation;
using MediatR;
using TenantAdministration.Application.Authorization;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Command para suspender um tenant. Restrito ao Platform Operator.
/// Carrega o agregado via repositório, invoca <c>Tenant.Suspend()</c>
/// e persiste com Outbox (via <c>TransactionBehavior</c>).
/// design.md §5.1 e §8.1 POST /api/v1/platform/tenants/{tenantId}/suspend.
/// </summary>
[RequiresPlatformOperator]
public sealed record SuspendTenantCommand(Guid TenantId, string? Reason = null)
    : IRequest<SuspendTenantResult>;

/// <summary>Resultado da operação de suspensão.</summary>
/// <param name="TenantId">Identificador do tenant suspenso.</param>
/// <param name="Status">Estado resultante (<c>suspended</c>).</param>
public sealed record SuspendTenantResult(Guid TenantId, string Status);

/// <summary>Validador de <see cref="SuspendTenantCommand"/>.</summary>
public sealed class SuspendTenantCommandValidator : AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEqual(Guid.Empty)
            .WithMessage("TenantId é obrigatório.");
    }
}
