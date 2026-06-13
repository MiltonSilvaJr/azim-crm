using MediatR;
using Microsoft.AspNetCore.Mvc;
using TenantAdministration.Application.Commands;
using TenantAdministration.Application.Ports;
using TenantAdministration.Application.Queries;
using TenantAdministration.Contracts.Tenant;
namespace TenantAdministration.Api.Controllers;

/// <summary>
/// Controller REST para endpoints do plano de tenant (Tenant Admin / Viewer).
/// Rota base: <c>/api/v1/tenant</c>.
/// Todos os endpoints são isolados por tenant via RLS (design.md §8.2, Req 10).
/// Não contém lógica de negócio — delega ao MediatR.
/// </summary>
[ApiController]
[Route("api/v1/tenant")]
[Produces("application/json")]
public sealed class TenantController(
    IMediator mediator,
    ITenantContext tenantContext) : ControllerBase
{
    /// <summary>
    /// Obtém os dados do tenant corrente. Acessível por Viewer e superiores.
    /// Não expõe identityTenantId nem adminEmail (Req 9.2).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentTenant(CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new UnauthorizedAccessException("Contexto de tenant ausente.");

        var query = new GetCurrentTenantQuery(tenantId);
        var dto = await mediator.Send(query, ct);

        return Ok(new TenantResponse(
            TenantId: dto.TenantId,
            Slug: dto.Slug,
            DisplayName: dto.DisplayName,
            Timezone: dto.Timezone,
            DigestTime: dto.DigestTime,
            Status: dto.Status));
    }

    /// <summary>
    /// Atualiza displayName, timezone e digestTime do tenant corrente.
    /// Nunca aceita slug no corpo (TA-ERR-011 — Req 2.3).
    /// Exige papel <c>TenantAdmin</c>.
    /// </summary>
    [HttpPatch]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDigestConfig(
        [FromBody] UpdateDigestConfigRequest request,
        CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new UnauthorizedAccessException("Contexto de tenant ausente.");

        var command = new UpdateDigestConfigCommand(
            TenantId: tenantId,
            Timezone: request.Timezone,
            DigestTime: request.DigestTime,
            Slug: request.Slug);  // TA-ERR-011 se não nulo (validado no handler)

        var result = await mediator.Send(command, ct);

        // Recarrega para retornar dados completos
        var query = new GetCurrentTenantQuery(result.TenantId);
        var dto = await mediator.Send(query, ct);

        return Ok(new TenantResponse(
            TenantId: dto.TenantId,
            Slug: dto.Slug,
            DisplayName: dto.DisplayName,
            Timezone: dto.Timezone,
            DigestTime: dto.DigestTime,
            Status: dto.Status));
    }

    /// <summary>
    /// Obtém o branding do tenant corrente com tons derivados.
    /// Acessível por Viewer e superiores.
    /// </summary>
    [HttpGet("branding")]
    [ProducesResponseType(typeof(BrandingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBranding(CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new UnauthorizedAccessException("Contexto de tenant ausente.");

        var query = new GetTenantBrandingQuery(tenantId);
        var dto = await mediator.Send(query, ct);

        return Ok(MapBrandingResponse(dto));
    }

    /// <summary>
    /// Atualiza o branding white-label do tenant corrente.
    /// Aceita multipart/form-data com logo, favicon e cores.
    /// Exige papel <c>TenantAdmin</c>.
    /// Rejeições não alteram branding prévio (Req 7.5).
    /// </summary>
    [HttpPut("branding")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UpdateBrandingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413RequestEntityTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateBranding(
        IFormFile? logo,
        IFormFile? favicon,
        [FromForm] string primaryColor,
        [FromForm] string secondaryColor,
        CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new UnauthorizedAccessException("Contexto de tenant ausente.");

        var command = new UpdateBrandingCommand(
            TenantId: tenantId,
            Logo: logo?.OpenReadStream(),
            LogoSizeBytes: logo?.Length ?? 0,
            LogoMediaType: logo?.ContentType,
            Favicon: favicon?.OpenReadStream(),
            FaviconSizeBytes: favicon?.Length ?? 0,
            FaviconMediaType: favicon?.ContentType,
            PrimaryColor: primaryColor,
            SecondaryColor: secondaryColor);

        var result = await mediator.Send(command, ct);

        return Ok(new UpdateBrandingResponse(
            WcagContrastOk: result.WcagContrastOk,
            ContrastRatio: result.ContrastRatio,
            LogoUrl: result.LogoUrl,
            FaviconUrl: result.FaviconUrl,
            DerivedTones: new DerivedTonesResponse(
                PrimaryHover: result.DerivedTones.PrimaryHover,
                PrimaryActive: result.DerivedTones.PrimaryActive,
                PrimaryMuted: result.DerivedTones.PrimaryMuted,
                SecondaryHover: result.DerivedTones.SecondaryHover,
                SecondaryActive: result.DerivedTones.SecondaryActive,
                SecondaryMuted: result.DerivedTones.SecondaryMuted)));
    }

    private static BrandingResponse MapBrandingResponse(TenantBrandingDto dto) =>
        new(
            TenantId: dto.TenantId,
            LogoUrl: dto.LogoUrl,
            FaviconUrl: dto.FaviconUrl,
            Colors: dto.PrimaryColor is not null && dto.SecondaryColor is not null
                ? new ColorsResponse(dto.PrimaryColor, dto.SecondaryColor)
                : null,
            WcagContrastOk: dto.WcagContrastOk,
            LastContrastRatio: dto.LastContrastRatio,
            DerivedTones: dto.DerivedTones is not null
                ? new DerivedTonesResponse(
                    PrimaryHover: dto.DerivedTones.PrimaryHover,
                    PrimaryActive: dto.DerivedTones.PrimaryActive,
                    PrimaryMuted: dto.DerivedTones.PrimaryMuted,
                    SecondaryHover: dto.DerivedTones.SecondaryHover,
                    SecondaryActive: dto.DerivedTones.SecondaryActive,
                    SecondaryMuted: dto.DerivedTones.SecondaryMuted)
                : null,
            UpdatedAt: dto.UpdatedAt);
}
