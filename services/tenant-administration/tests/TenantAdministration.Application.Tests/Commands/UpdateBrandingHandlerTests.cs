using System.Text;
using FluentAssertions;
using NSubstitute;
using TenantAdministration.Application.Commands;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Application.Ports;
using TenantAdministration.Domain.Aggregates;
using TenantAdministration.Domain.ValueObjects;
using Xunit;

namespace TenantAdministration.Application.Tests.Commands;

public sealed class UpdateBrandingHandlerTests
{
    private readonly ITenantRepository _repository = Substitute.For<ITenantRepository>();
    private readonly IBrandingAssetStorage _assetStorage = Substitute.For<IBrandingAssetStorage>();
    private readonly ICdnInvalidator _cdnInvalidator = Substitute.For<ICdnInvalidator>();
    private readonly IEventOutbox _outbox = Substitute.For<IEventOutbox>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid TenantId = Guid.NewGuid();

    private UpdateBrandingHandler CreateHandler() =>
        new(_repository, _assetStorage, _cdnInvalidator, _outbox, _clock);

    private Tenant CreateTenant()
    {
        var slug = Slug.Create("meu-tenant").Value;
        var tz = TimezoneIana.Create("America/Sao_Paulo").Value;
        return Tenant.Provision(slug, "Meu Tenant", tz, DigestTime.Default, "admin@a.com", Now);
    }

    // PNG magic bytes válidos
    private static Stream PngStream() =>
        new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00]);

    private static UpdateBrandingCommand ValidCommand(Guid tenantId) =>
        new(
            TenantId: tenantId,
            Logo: PngStream(),
            LogoSizeBytes: 100,
            LogoMediaType: "image/png",
            Favicon: null,
            FaviconSizeBytes: 0,
            FaviconMediaType: null,
            PrimaryColor: "#000000",
            SecondaryColor: "#FFFFFF");

    // ──────────────────────────────────────────────────────────────
    // Cenário de sucesso
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Branding válido: WcagContrastOk=true, derivedTones e CDN invalidado")]
    public async Task ValidBranding_ShouldReturnOkAndInvalidateCdn()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateTenant();
        tenant.ClearDomainEvents();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        _assetStorage.UploadAsync(tenant.Id, "meu-tenant", "logo", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.azim.com.br/tenants/meu-tenant/logo.png");

        var handler = CreateHandler();
        var result = await handler.Handle(ValidCommand(tenant.Id), CancellationToken.None);

        result.WcagContrastOk.Should().BeTrue();
        result.ContrastRatio.Should().BeGreaterThan(4.5m);
        result.LogoUrl.Should().Be("https://cdn.azim.com.br/tenants/meu-tenant/logo.png");
        result.DerivedTones.Should().NotBeNull();
        await _cdnInvalidator.Received(1).InvalidateAsync("meu-tenant", Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-012: arquivo muito grande
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TA-ERR-012: logo maior que 1 MB → rejeição antes do upload GCS")]
    public async Task OversizedLogo_ShouldThrow_TA_ERR_012_BeforeUpload()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateTenant();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var cmd = new UpdateBrandingCommand(
            TenantId: tenant.Id,
            Logo: PngStream(),
            LogoSizeBytes: 2_000_000, // > 1 MB
            LogoMediaType: "image/png",
            Favicon: null,
            FaviconSizeBytes: 0,
            FaviconMediaType: null,
            PrimaryColor: "#000000",
            SecondaryColor: "#FFFFFF");

        var handler = CreateHandler();
        var act = () => handler.Handle(cmd, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-012");
        await _assetStorage.DidNotReceive()
            .UploadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-013: formato não suportado
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TA-ERR-013: JPEG enviado → rejeição antes do upload GCS")]
    public async Task JpegLogo_ShouldThrow_TA_ERR_013_BeforeUpload()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateTenant();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        // JPEG magic bytes: FF D8 FF
        var jpegStream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        var cmd = new UpdateBrandingCommand(
            TenantId: tenant.Id,
            Logo: jpegStream,
            LogoSizeBytes: 100,
            LogoMediaType: "image/jpeg",
            Favicon: null,
            FaviconSizeBytes: 0,
            FaviconMediaType: null,
            PrimaryColor: "#000000",
            SecondaryColor: "#FFFFFF");

        var handler = CreateHandler();
        var act = () => handler.Handle(cmd, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-013");
        await _assetStorage.DidNotReceive()
            .UploadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────
    // TA-ERR-014: contraste insuficiente
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "TA-ERR-014: cores com contraste < 4.5 → bloqueio com ratio no erro")]
    public async Task InsufficientContrast_ShouldThrow_TA_ERR_014_WithRatio()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateTenant();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        // Mesma cor → contraste 1:1
        var cmd = new UpdateBrandingCommand(
            TenantId: tenant.Id,
            Logo: null,
            LogoSizeBytes: 0,
            LogoMediaType: null,
            Favicon: null,
            FaviconSizeBytes: 0,
            FaviconMediaType: null,
            PrimaryColor: "#888888",
            SecondaryColor: "#999999");

        var handler = CreateHandler();
        var act = () => handler.Handle(cmd, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.ErrorCode.Should().Be("TA-ERR-014");
        ex.Which.Message.Should().Contain("1,");  // ratio baixo presente na mensagem
    }

    // ──────────────────────────────────────────────────────────────
    // Branding anterior preservado em caso de falha
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Falha no upload GCS: branding anterior não é alterado")]
    public async Task GcsUploadFailure_ShouldPreservePreviousBranding()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateTenant();
        tenant.ClearDomainEvents();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        _assetStorage.UploadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new InvalidOperationException("GCS indisponível"));

        var handler = CreateHandler();
        var act = () => handler.Handle(ValidCommand(tenant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // Branding não foi aplicado ao agregado
        tenant.Branding.Should().BeNull();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
        await _cdnInvalidator.DidNotReceive().InvalidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────
    // ICdnInvalidator chamado uma vez por atualização bem-sucedida
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "ICdnInvalidator é chamado exatamente uma vez após commit bem-sucedido")]
    public async Task SuccessfulUpdate_CdnInvalidatedOnce()
    {
        _clock.UtcNow.Returns(Now);
        var tenant = CreateTenant();
        tenant.ClearDomainEvents();
        _repository.FindByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        _assetStorage.UploadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.azim.com.br/logo.png");

        var handler = CreateHandler();
        await handler.Handle(ValidCommand(tenant.Id), CancellationToken.None);

        await _cdnInvalidator.Received(1).InvalidateAsync("meu-tenant", Arg.Any<CancellationToken>());
    }
}
