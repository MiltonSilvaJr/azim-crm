using MediatR;
using Microsoft.AspNetCore.Mvc;
using TenantAdministration.Application.Queries;
using TenantAdministration.Contracts.Public;
using TenantAdministration.Contracts.Tenant;

namespace TenantAdministration.Api.Controllers;

/// <summary>
/// Controller REST para o endpoint público de branding cacheável (brand.json).
/// Rota: <c>/brand/{slug}/brand.json</c>.
/// Sem autenticação (público). Implementa anti-enumeração: slug inexistente e tenant
/// suspenso retornam o mesmo 404 (RNF 1, design.md §8.3).
/// </summary>
[ApiController]
[Route("brand")]
[Produces("application/json")]
public sealed class BrandController(IMediator mediator) : ControllerBase
{
    private const int CacheTtlSeconds = 300;

    /// <summary>
    /// Retorna o brand.json público de um tenant identificado pelo slug.
    /// Cache: <c>Cache-Control: public, max-age=300</c>; ETag baseado em <c>version</c>.
    /// Anti-enumeração: slug inexistente = suspenso = 404 com TA-ERR-008.
    /// </summary>
    /// <param name="slug">Slug único do tenant.</param>
    /// <param name="ct">Token de cancelamento.</param>
    [HttpGet("{slug}/brand.json")]
    [ResponseCache(Duration = CacheTtlSeconds, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(typeof(BrandJsonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBrandJson(
        [FromRoute] string slug,
        CancellationToken ct)
    {
        var query = new GetPublicBrandJsonQuery(slug);
        var dto = await mediator.Send(query, ct);

        // ETag derivado do timestamp de versão para suportar 304 Not Modified
        var eTag = $"\"{dto.Version.ToUnixTimeMilliseconds()}\"";

        // Verificar If-None-Match para 304 Not Modified
        var ifNoneMatch = Request.Headers.IfNoneMatch.FirstOrDefault();
        if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == eTag)
        {
            Response.Headers.ETag = eTag;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        // Headers de cache CDN (Req 9.3, RNF 4)
        Response.Headers.CacheControl = $"public, max-age={CacheTtlSeconds}";
        Response.Headers.ETag = eTag;

        var response = new BrandJsonResponse(
            Slug: dto.Slug,
            LogoUrl: dto.LogoUrl,
            FaviconUrl: dto.FaviconUrl,
            Colors: dto.Colors is not null
                ? new ColorsResponse(dto.Colors.Primary, dto.Colors.Secondary)
                : null,
            WcagContrastOk: dto.WcagContrastOk,
            DerivedTones: dto.DerivedTones is not null
                ? new DerivedTonesResponse(
                    PrimaryHover: dto.DerivedTones.PrimaryHover,
                    PrimaryActive: dto.DerivedTones.PrimaryActive,
                    PrimaryMuted: dto.DerivedTones.PrimaryMuted,
                    SecondaryHover: dto.DerivedTones.SecondaryHover,
                    SecondaryActive: dto.DerivedTones.SecondaryActive,
                    SecondaryMuted: dto.DerivedTones.SecondaryMuted)
                : null,
            Version: dto.Version);

        return Ok(response);
    }
}
