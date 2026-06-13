using MediatR;
using TenantAdministration.Application.Authorization;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Application.Queries;

/// <summary>
/// Dados do tenant corrente para leitura (sem dados sensíveis).
/// </summary>
public sealed record TenantDto(
    Guid TenantId,
    string Slug,
    string DisplayName,
    string Timezone,
    string DigestTime,
    string Status);

/// <summary>
/// Query para obter os dados do tenant corrente. Acessível por Viewer e superiores.
/// Isolado por RLS (contexto de tenant — Req 10).
/// design.md §5.2 e §8.2 GET /api/v1/tenant.
/// </summary>
[RequiresTenantAdmin]
public sealed record GetCurrentTenantQuery(Guid TenantId) : IRequest<TenantDto>;

/// <summary>
/// Handler de <see cref="GetCurrentTenantQuery"/>.
/// </summary>
public sealed class GetCurrentTenantHandler(ITenantRepository repository)
    : IRequestHandler<GetCurrentTenantQuery, TenantDto>
{
    /// <inheritdoc/>
    public async Task<TenantDto> Handle(
        GetCurrentTenantQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await repository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
            throw new DomainValidationException(
                "TA-ERR-008",
                "Tenant não encontrado. Verifique o identificador informado.");

        return new TenantDto(
            TenantId: tenant.Id,
            Slug: tenant.Slug.Value,
            DisplayName: tenant.DisplayName,
            Timezone: tenant.Timezone.Value,
            DigestTime: tenant.DigestTime.Value,
            Status: tenant.Status.ToString().ToLowerInvariant());
    }
}
