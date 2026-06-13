using FluentValidation;
using MediatR;
using TenantAdministration.Application.Authorization;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Command para reativar um tenant suspenso. Restrito ao Platform Operator.
/// Carrega o agregado via repositório, invoca <c>Tenant.Reactivate()</c>
/// e persiste com Outbox (via <c>TransactionBehavior</c>).
/// design.md §5.1 e §8.1 POST /api/v1/platform/tenants/{tenantId}/reactivate.
/// </summary>
[RequiresPlatformOperator]
public sealed record ReactivateTenantCommand(Guid TenantId) : IRequest<ReactivateTenantResult>;

/// <summary>Resultado da operação de reativação.</summary>
/// <param name="TenantId">Identificador do tenant reativado.</param>
/// <param name="Status">Estado resultante (<c>provisioned</c>).</param>
public sealed record ReactivateTenantResult(Guid TenantId, string Status);

/// <summary>Validador de <see cref="ReactivateTenantCommand"/>.</summary>
public sealed class ReactivateTenantCommandValidator : AbstractValidator<ReactivateTenantCommand>
{
    public ReactivateTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEqual(Guid.Empty)
            .WithMessage("TenantId é obrigatório.");
    }
}
