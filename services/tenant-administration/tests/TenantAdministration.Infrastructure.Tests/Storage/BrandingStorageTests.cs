using FluentAssertions;
using TenantAdministration.Infrastructure.Storage;
using Xunit;

namespace TenantAdministration.Infrastructure.Tests.Storage;

/// <summary>
/// Testes unitários dos adapters de storage e CDN (TASK-14).
/// Usa fakes — sem dependência de cloud real.
/// </summary>
public sealed class BrandingStorageTests
{
    [Fact(DisplayName = "FakeBrandingAssetStorage retorna URL CDN com path correto")]
    public async Task FakeStorage_UploadAsync_ReturnsCdnUrlWithCorrectPath()
    {
        // Arrange
        var storage = new FakeBrandingAssetStorage();
        var tenantId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG magic bytes

        // Act
        var url = await storage.UploadAsync(tenantId, "my-tenant", "logo", stream);

        // Assert
        url.Should().Contain("tenants/my-tenant/logo");
        storage.Uploads.Should().HaveCount(1);
        storage.Uploads[0].TenantId.Should().Be(tenantId);
        storage.Uploads[0].Type.Should().Be("logo");
    }

    [Fact(DisplayName = "FakeBrandingAssetStorage.ShouldFail lança exceção no upload")]
    public async Task FakeStorage_ShouldFail_ThrowsOnUpload()
    {
        // Arrange
        var storage = new FakeBrandingAssetStorage { ShouldFail = true };
        using var stream = new MemoryStream([]);

        // Act
        var act = async () => await storage.UploadAsync(Guid.NewGuid(), "slug", "logo", stream);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "FakeCdnInvalidator registra slug invalidado")]
    public async Task FakeCdn_InvalidateAsync_RecordsSlug()
    {
        // Arrange
        var cdn = new FakeCdnInvalidator();

        // Act
        await cdn.InvalidateAsync("my-tenant");
        await cdn.InvalidateAsync("other-tenant");

        // Assert
        cdn.Invalidated.Should().Contain("my-tenant");
        cdn.Invalidated.Should().Contain("other-tenant");
        cdn.Invalidated.Should().HaveCount(2);
    }

    [Fact(DisplayName = "FakeCdnInvalidator.ShouldFail lança exceção")]
    public async Task FakeCdn_ShouldFail_Throws()
    {
        // Arrange
        var cdn = new FakeCdnInvalidator { ShouldFail = true };

        // Act
        var act = async () => await cdn.InvalidateAsync("slug");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "Upload de logo e favicon geram paths distintos por type")]
    public async Task FakeStorage_UploadAsync_GeneratesDistinctPathsByType()
    {
        // Arrange
        var storage = new FakeBrandingAssetStorage();
        var tenantId = Guid.NewGuid();
        using var logoStream = new MemoryStream([]);
        using var faviconStream = new MemoryStream([]);

        // Act
        var logoUrl = await storage.UploadAsync(tenantId, "my-brand", "logo", logoStream);
        var faviconUrl = await storage.UploadAsync(tenantId, "my-brand", "favicon", faviconStream);

        // Assert
        logoUrl.Should().Contain("/logo");
        faviconUrl.Should().Contain("/favicon");
        logoUrl.Should().NotBe(faviconUrl);
    }

    [Fact(DisplayName = "Upload idempotente — mesmo path não falha em chamadas repetidas")]
    public async Task FakeStorage_UploadAsync_IsIdempotentByPath()
    {
        // Arrange
        var storage = new FakeBrandingAssetStorage();
        var tenantId = Guid.NewGuid();

        // Act — dois uploads para o mesmo path (logo)
        var url1 = await storage.UploadAsync(tenantId, "my-tenant", "logo", new MemoryStream([]));
        var url2 = await storage.UploadAsync(tenantId, "my-tenant", "logo", new MemoryStream([]));

        // Assert — ambas retornam a mesma URL CDN (idempotente por path)
        url1.Should().Be(url2);
    }
}
