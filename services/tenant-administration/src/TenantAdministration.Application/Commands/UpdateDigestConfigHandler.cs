using MediatR;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Handler do command <see cref="UpdateDigestConfigCommand"/>.
/// Valida fuso horário IANA (TA-ERR-006), carrega o agregado,
/// invoca <c>Tenant.UpdateDigestConfig()</c> e persiste com Outbox.
/// design.md §5.3.
/// </summary>
public sealed class UpdateDigestConfigHandler(
    ITenantRepository repository,
    IEventOutbox outbox,
    IClock clock)
    : IRequestHandler<UpdateDigestConfigCommand, UpdateDigestConfigResult>
{
    /// <inheritdoc/>
    public async Task<UpdateDigestConfigResult> Handle(
        UpdateDigestConfigCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validar fuso horário IANA (TA-ERR-006)
        var timezoneResult = TimezoneIana.Create(request.Timezone);
        if (timezoneResult.IsFailure)
            throw new DomainValidationException(timezoneResult.ErrorCode!, timezoneResult.ErrorMessage!);

        // 2. Validar DigestTime
        var digestTimeResult = DigestTime.Create(request.DigestTime);
        if (digestTimeResult.IsFailure)
            throw new DomainValidationException(digestTimeResult.ErrorCode!, digestTimeResult.ErrorMessage!);

        // 3. Carregar o agregado (TA-ERR-008)
        var tenant = await repository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
            throw new DomainValidationException(
                "TA-ERR-008",
                "Tenant não encontrado. Verifique o identificador informado.");

        // 4. Aplicar a atualização no agregado
        tenant.UpdateDigestConfig(timezoneResult.Value, digestTimeResult.Value, clock.UtcNow);

        // 5. Persistir o agregado
        await repository.UpdateAsync(tenant, cancellationToken);

        // 6. Enfileirar domain events no Outbox
        await outbox.AppendRangeAsync(tenant.DomainEvents, cancellationToken);
        tenant.ClearDomainEvents();

        return new UpdateDigestConfigResult(
            tenant.Id,
            timezoneResult.Value.Value,
            digestTimeResult.Value.Value);
    }
}
