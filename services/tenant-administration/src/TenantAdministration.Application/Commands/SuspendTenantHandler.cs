using MediatR;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Handler do command <see cref="SuspendTenantCommand"/>.
/// Carrega o agregado <c>Tenant</c>, invoca <c>Tenant.Suspend()</c>,
/// persiste via repositório e enfileira eventos no Outbox (via <c>TransactionBehavior</c>).
/// Propaga TA-ERR-007 (transição inválida) e TA-ERR-008 (não encontrado).
/// design.md §5.3.
/// </summary>
public sealed class SuspendTenantHandler(
    ITenantRepository repository,
    IEventOutbox outbox,
    IClock clock)
    : IRequestHandler<SuspendTenantCommand, SuspendTenantResult>
{
    /// <inheritdoc/>
    public async Task<SuspendTenantResult> Handle(
        SuspendTenantCommand request,
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
            tenant.Suspend(clock.UtcNow);
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

        return new SuspendTenantResult(tenant.Id, "suspended");
    }
}
