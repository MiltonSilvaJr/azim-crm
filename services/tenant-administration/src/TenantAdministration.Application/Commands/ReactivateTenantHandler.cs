using MediatR;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Handler do command <see cref="ReactivateTenantCommand"/>.
/// Carrega o agregado <c>Tenant</c>, invoca <c>Tenant.Reactivate()</c>,
/// persiste via repositório e enfileira eventos no Outbox (via <c>TransactionBehavior</c>).
/// Propaga TA-ERR-007 (transição inválida) e TA-ERR-008 (não encontrado).
/// design.md §5.3.
/// </summary>
public sealed class ReactivateTenantHandler(
    ITenantRepository repository,
    IEventOutbox outbox,
    IClock clock)
    : IRequestHandler<ReactivateTenantCommand, ReactivateTenantResult>
{
    /// <inheritdoc/>
    public async Task<ReactivateTenantResult> Handle(
        ReactivateTenantCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Carregar o agregado (TA-ERR-008)
        var tenant = await repository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
            throw new DomainValidationException(
                "TA-ERR-008",
                "Tenant não encontrado. Verifique o identificador informado.");

        // 2. Invocar a transição de estado no agregado (TA-ERR-007 se inválida)
        try
        {
            tenant.Reactivate(clock.UtcNow);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("TA-ERR-007"))
        {
            throw new DomainValidationException(
                "TA-ERR-007",
                "Transição de estado inválida. Verifique o estado atual do tenant.");
        }

        // 3. Persistir o agregado
        await repository.UpdateAsync(tenant, cancellationToken);

        // 4. Enfileirar domain events no Outbox (TransactionBehavior confirma na mesma transação)
        await outbox.AppendRangeAsync(tenant.DomainEvents, cancellationToken);
        tenant.ClearDomainEvents();

        return new ReactivateTenantResult(tenant.Id, "provisioned");
    }
}
