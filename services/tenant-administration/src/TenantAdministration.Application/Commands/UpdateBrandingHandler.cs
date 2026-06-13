using MediatR;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Policies;
using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Application.Commands;

/// <summary>
/// Handler do command <see cref="UpdateBrandingCommand"/>.
/// Orquestra conforme o sequence diagram do design.md §16.4:
/// (1) AssetValidationPolicy — rejeita asset inválido antes de qualquer I/O;
/// (2) Upload via IBrandingAssetStorage;
/// (3) WcagContrastPolicy — bloqueia cores insuficientes (TA-ERR-014, DD-004);
/// (4) Tenant.UpdateBranding — aplica a mudança no agregado;
/// (5) Persistir via ITenantRepository + enfileirar Outbox;
/// (6) ICdnInvalidator — invalida brand.json.
/// Rejeições antes do commit não alteram o branding prévio (Req 7.5).
/// design.md §5.3.
/// </summary>
public sealed class UpdateBrandingHandler(
    ITenantRepository repository,
    IBrandingAssetStorage assetStorage,
    ICdnInvalidator cdnInvalidator,
    IEventOutbox outbox,
    IClock clock)
    : IRequestHandler<UpdateBrandingCommand, UpdateBrandingResult>
{
    /// <inheritdoc/>
    public async Task<UpdateBrandingResult> Handle(
        UpdateBrandingCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Carregar o agregado (TA-ERR-008)
        var tenant = await repository.FindByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
            throw new DomainValidationException(
                "TA-ERR-008",
                "Tenant não encontrado. Verifique o identificador informado.");

        // 2. Validar assets ANTES de qualquer I/O (TA-ERR-012/013)
        if (request.Logo is not null)
        {
            var logoValidation = AssetValidationPolicy.Validate(
                request.Logo, request.LogoMediaType ?? string.Empty, request.LogoSizeBytes);
            if (logoValidation.IsFailure)
                throw new DomainValidationException(logoValidation.ErrorCode!, logoValidation.ErrorMessage!);
        }

        if (request.Favicon is not null)
        {
            var faviconValidation = AssetValidationPolicy.Validate(
                request.Favicon, request.FaviconMediaType ?? string.Empty, request.FaviconSizeBytes);
            if (faviconValidation.IsFailure)
                throw new DomainValidationException(faviconValidation.ErrorCode!, faviconValidation.ErrorMessage!);
        }

        // 3. Validar cores (ColorPair — TA-ERR-004)
        var colorResult = ColorPair.Create(request.PrimaryColor, request.SecondaryColor);
        if (colorResult.IsFailure)
            throw new DomainValidationException(colorResult.ErrorCode!, colorResult.ErrorMessage!);

        var colors = colorResult.Value;

        // 4. Verificar contraste WCAG AA antes do upload (TA-ERR-014, DD-004: bloquear)
        var (wcagApproved, contrastRatio) = WcagContrastPolicy.Check(colors);
        if (!wcagApproved)
            throw new DomainValidationException(
                "TA-ERR-014",
                $"Contraste insuficiente para conformidade WCAG AA. Razão calculada: {contrastRatio:F2}:1.");

        // 5. Upload de assets (somente após validações passarem)
        string? logoUrl = tenant.Branding?.Theme.LogoUrl;
        string? faviconUrl = tenant.Branding?.Theme.FaviconUrl;

        if (request.Logo is not null)
            logoUrl = await assetStorage.UploadAsync(
                tenant.Id, tenant.Slug.Value, "logo", request.Logo, cancellationToken);

        if (request.Favicon is not null)
            faviconUrl = await assetStorage.UploadAsync(
                tenant.Id, tenant.Slug.Value, "favicon", request.Favicon, cancellationToken);

        // 6. Aplicar branding no agregado
        var theme = BrandingTheme.Create(logoUrl, faviconUrl, colors);
        tenant.UpdateBranding(theme, wcagApproved, contrastRatio, clock.UtcNow);

        // 7. Persistir o agregado
        await repository.UpdateAsync(tenant, cancellationToken);

        // 8. Enfileirar domain events no Outbox
        await outbox.AppendRangeAsync(tenant.DomainEvents, cancellationToken);
        tenant.ClearDomainEvents();

        // 9. Invalidar CDN após commit bem-sucedido
        await cdnInvalidator.InvalidateAsync(tenant.Slug.Value, cancellationToken);

        // 10. Derivar tons deterministicamente
        var derivedTones = ToneDerivationService.Derive(colors);

        return new UpdateBrandingResult(wcagApproved, contrastRatio, logoUrl, faviconUrl, derivedTones);
    }
}
