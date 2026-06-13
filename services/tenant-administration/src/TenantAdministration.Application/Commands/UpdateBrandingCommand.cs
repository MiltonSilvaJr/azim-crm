using FluentValidation;
using MediatR;
using TenantAdministration.Application.Authorization;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Resultado da atualização de branding.
/// </summary>
/// <param name="WcagContrastOk">Indica se as cores atendem ao contraste WCAG AA.</param>
/// <param name="ContrastRatio">Razão de contraste calculada (arredondada a 2 casas).</param>
/// <param name="LogoUrl">URL CDN do logo após upload.</param>
/// <param name="FaviconUrl">URL CDN do favicon após upload.</param>
/// <param name="DerivedTones">Tons HSL derivados deterministicamente das cores.</param>
public sealed record UpdateBrandingResult(
    bool WcagContrastOk,
    decimal ContrastRatio,
    string? LogoUrl,
    string? FaviconUrl,
    DerivedTones DerivedTones);

/// <summary>
/// Command para atualizar o branding white-label de um tenant. Restrito ao Tenant Admin.
/// Orquestra validação de asset, upload GCS, validação WCAG, persistência e CDN.
/// design.md §5.1, §5.3 e §8.2 PUT /api/v1/tenant/branding.
/// </summary>
[RequiresTenantAdmin]
public sealed record UpdateBrandingCommand(
    Guid TenantId,
    Stream? Logo,
    long LogoSizeBytes,
    string? LogoMediaType,
    Stream? Favicon,
    long FaviconSizeBytes,
    string? FaviconMediaType,
    string PrimaryColor,
    string SecondaryColor) : IRequest<UpdateBrandingResult>;

/// <summary>
/// Validador de <see cref="UpdateBrandingCommand"/>.
/// Verifica formato de cor (#RRGGBB) na borda.
/// </summary>
public sealed class UpdateBrandingCommandValidator : AbstractValidator<UpdateBrandingCommand>
{
    private static readonly System.Text.RegularExpressions.Regex HexPattern =
        new(@"^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public UpdateBrandingCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEqual(Guid.Empty)
            .WithMessage("TenantId é obrigatório.");

        RuleFor(x => x.PrimaryColor)
            .NotEmpty()
            .WithErrorCode("TA-ERR-004")
            .WithMessage("Cor primária é obrigatória.")
            .Matches(HexPattern)
            .WithErrorCode("TA-ERR-004")
            .WithMessage("Cor primária inválida; use o formato #RRGGBB.")
            .When(x => !string.IsNullOrWhiteSpace(x.PrimaryColor));

        RuleFor(x => x.SecondaryColor)
            .NotEmpty()
            .WithErrorCode("TA-ERR-004")
            .WithMessage("Cor secundária é obrigatória.")
            .Matches(HexPattern)
            .WithErrorCode("TA-ERR-004")
            .WithMessage("Cor secundária inválida; use o formato #RRGGBB.")
            .When(x => !string.IsNullOrWhiteSpace(x.SecondaryColor));
    }
}
