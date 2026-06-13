using MediatR;
using TenantAdministration.Application.Authorization;
using TenantAdministration.Application.Dtos;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Policies;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Application.Queries;

/// <summary>
/// Payload público do brand.json. Sem dados sensíveis (Req 9.2).
/// Não contém adminEmail, identityTenantId nem dados de plano de plataforma.
/// </summary>
public sealed record PublicBrandJsonDto(
    string Slug,
    string? LogoUrl,
    string? FaviconUrl,
    ColorsDto? Colors,
    bool WcagContrastOk,
    DerivedTonesDto? DerivedTones,
    DateTimeOffset Version);

/// <summary>Par de cores público para o brand.json.</summary>
public sealed record ColorsDto(string Primary, string Secondary);

/// <summary>
/// Query pública para obter o brand.json de um tenant por slug.
/// Acesso anônimo (sem dados sensíveis — Req 9.2).
/// Erro 404 sem distinção entre inexistente e suspenso (anti-enumeração — RNF 1).
/// design.md §5.2 e §8.3 GET /brand/{slug}/brand.json.
/// </summary>
[AllowAnonymous]
public sealed record GetPublicBrandJsonQuery(string Slug) : IRequest<PublicBrandJsonDto>;

/// <summary>
/// Handler de <see cref="GetPublicBrandJsonQuery"/>.
/// Nunca retorna adminEmail, identityTenantId ou dados internos do plano de plataforma.
/// </summary>
public sealed class GetPublicBrandJsonHandler(ITenantRepository repository)
    : IRequestHandler<GetPublicBrandJsonQuery, PublicBrandJsonDto>
{
    /// <inheritdoc/>
    public async Task<PublicBrandJsonDto> Handle(
        GetPublicBrandJsonQuery request,
        CancellationToken cancellationToken)
    {
        // Resolve tenant por slug (sem distinção de inexistente/suspenso — anti-enumeração)
        var tenant = await repository.FindBySlugAsync(request.Slug, cancellationToken);
        if (tenant is null || !tenant.Active)
            throw new DomainValidationException(
                "TA-ERR-008",
                "Tenant não encontrado.");  // não revelar se suspenso ou inexistente (RNF 1)

        var branding = tenant.Branding;
        DerivedTonesDto? derivedTonesDto = null;
        ColorsDto? colors = null;

        if (branding?.Theme.Colors is not null)
        {
            var domainTones = ToneDerivationService.Derive(branding.Theme.Colors);
            derivedTonesDto = new DerivedTonesDto(
                domainTones.PrimaryHover,
                domainTones.PrimaryActive,
                domainTones.PrimaryMuted,
                domainTones.SecondaryHover,
                domainTones.SecondaryActive,
                domainTones.SecondaryMuted);
            colors = new ColorsDto(branding.Theme.Colors.Primary, branding.Theme.Colors.Secondary);
        }

        return new PublicBrandJsonDto(
            Slug: tenant.Slug.Value,
            LogoUrl: branding?.Theme.LogoUrl,
            FaviconUrl: branding?.Theme.FaviconUrl,
            Colors: colors,
            WcagContrastOk: branding?.WcagContrastOk ?? false,
            DerivedTones: derivedTonesDto,
            Version: branding?.UpdatedAt ?? tenant.ProvisionedAt);  // versão para ETag do CDN
    }
}
