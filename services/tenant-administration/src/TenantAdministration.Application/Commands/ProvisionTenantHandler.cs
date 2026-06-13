using MediatR;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Handler do command <see cref="ProvisionTenantCommand"/>.
/// Orquestra a validação de slug, verificação de unicidade e delegação à saga
/// de provisionamento atômico (<see cref="ITenantProvisioningSaga"/>).
/// Não tem acesso direto a EF Core nem a GCP SDK — apenas ports.
/// design.md §5.3 e §6.4.
/// </summary>
public sealed class ProvisionTenantHandler(
    ITenantRepository repository,
    ITenantProvisioningSaga saga,
    IClock clock)
    : IRequestHandler<ProvisionTenantCommand, ProvisionTenantResult>
{
    /// <inheritdoc/>
    public async Task<ProvisionTenantResult> Handle(
        ProvisionTenantCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validação do objeto de valor Slug (TA-ERR-005)
        var slugResult = Slug.Create(request.Slug);
        if (slugResult.IsFailure)
            throw new DomainValidationException(slugResult.ErrorCode!, slugResult.ErrorMessage!);

        var slug = slugResult.Value;

        // 2. Confirmação de slug (TA-ERR-003) — redundante com o validator mas garante
        //    integridade no handler mesmo se o behavior de validation for bypassado
        if (!string.Equals(request.Slug, request.SlugConfirmation, StringComparison.Ordinal))
            throw new DomainValidationException(
                "TA-ERR-003",
                "Confirmação de slug não corresponde.");

        // 3. Verificação de unicidade (TA-ERR-002)
        var slugExists = await repository.ExistsSlugAsync(slug.Value, cancellationToken);
        if (slugExists)
            throw new DomainValidationException(
                "TA-ERR-002",
                "Slug já está em uso. Escolha outro identificador para o tenant.");

        // 4. Validação do fuso horário (TA-ERR-006)
        var timezoneResult = TimezoneIana.Create(request.Timezone);
        if (timezoneResult.IsFailure)
            throw new DomainValidationException(timezoneResult.ErrorCode!, timezoneResult.ErrorMessage!);

        // 5. Delegar à saga de provisionamento atômico
        //    A saga cria o tenant no IdP, persiste o agregado + Outbox e compensa em falha.
        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString("N");
        _ = clock.UtcNow; // usa IClock para eventual auditoria de início

        var result = await saga.ExecuteAsync(
            tenantId: Guid.NewGuid(),
            slug: slug.Value,
            displayName: request.DisplayName,
            timezone: timezoneResult.Value.Value,
            digestTime: request.DigestTime,
            adminEmail: request.AdminEmail,
            idempotencyKey: idempotencyKey,
            ct: cancellationToken);

        return new ProvisionTenantResult(result.TenantId, slug.Value, "provisioned");
    }
}
