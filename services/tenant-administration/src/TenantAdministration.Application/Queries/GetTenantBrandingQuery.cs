using MediatR;
using TenantAdministration.Application.Authorization;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Policies;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Application.Queries;

/// <summary>
/// Branding do tenant com tons derivados.
/// </summary>
public sealed record TenantBrandingDto(
    Guid TenantId,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    bool WcagContrastOk,
    decimal LastContrastRatio,
    DerivedTones? DerivedTones,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Query para obter o branding do tenant corrente com tons derivados.
/// Acessível por Viewer e superiores. Isolado por RLS.
/// design.md §5.2 e §8.2 GET /api/v1/tenant/branding.
/// </summary>
[RequiresTenantAdmin]
public sealed record GetTenantBrandingQuery(Guid TenantId) : IRequest<TenantBrandingDto>;

/// <summary>
/// Handler de <see cref="GetTenantBrandingQuery"/>.
/// </summary>
public sealed class GetTenantBrandingHandler(ITenantRepository repository)
    : IRequestHandler<GetTenantBrandingQuery, TenantBrandingDto>
{
    /// <inheritdoc/>
    public async Task<TenantBrandingDto> Handle(
        GetTenantBrandingQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await repository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
            throw new DomainValidationException(
                "TA-ERR-008",
                "Tenant não encontrado. Verifique o identificador informado.");

        var branding = tenant.Branding;
        DerivedTones? derivedTones = null;

        if (branding?.Theme.Colors is not null)
        {
            derivedTones = ToneDerivationService.Derive(branding.Theme.Colors);
        }

        return new TenantBrandingDto(
            TenantId: tenant.Id,
            LogoUrl: branding?.Theme.LogoUrl,
            FaviconUrl: branding?.Theme.FaviconUrl,
            PrimaryColor: branding?.Theme.Colors.Primary,
            SecondaryColor: branding?.Theme.Colors.Secondary,
            WcagContrastOk: branding?.WcagContrastOk ?? false,
            LastContrastRatio: branding?.LastContrastRatio ?? 0m,
            DerivedTones: derivedTones,
            UpdatedAt: branding?.UpdatedAt ?? DateTimeOffset.MinValue);
    }
}
