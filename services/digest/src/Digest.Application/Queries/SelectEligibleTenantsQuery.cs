using MediatR;

namespace Digest.Application.Queries;

/// <summary>
/// Query que seleciona os tenants elegíveis para disparo do digest no instante de referência.
/// Usa <c>IUserDirectoryPort</c> + <see cref="Domain.ValueObjects.TenantEligibility"/> (design §5.2, Req 2).
/// Executada antes de setar <c>app.current_tenant</c> (cross-tenant administrativo — design §5.2).
/// </summary>
/// <param name="ReferenceUtc">Instante UTC do disparo do Cloud Scheduler.</param>
public sealed record SelectEligibleTenantsQuery(DateTimeOffset ReferenceUtc)
    : IRequest<IReadOnlyList<Guid>>;
