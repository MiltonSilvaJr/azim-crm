using System.Text;
using FluentAssertions;
using TenantAdministration.Domain.Policies;
using Xunit;

namespace TenantAdministration.Api.Tests.Security;

/// <summary>
/// Testes de segurança: sanitização de SVG malicioso antes do armazenamento (TASK-21).
/// design.md §10, DD-007, TASK-04/TASK-21.
/// Gate: [Trait("Category","SecurityGate")].
/// </summary>
[Trait("Category", "SecurityGate")]
public sealed class SvgSanitizationTests
{
    [Fact(DisplayName = "SVG com <script>alert(1)</script> é rejeitado pela AssetValidationPolicy")]
    public void SvgWithScriptTag_IsRejected()
    {
        // Arrange — payload malicioso real conforme TASK-21 ST-01
        const string maliciousSvg =
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <script>alert(1)</script>
              <rect width="100" height="100" fill="#fff"/>
            </svg>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(maliciousSvg));

        // Act
        var result = AssetValidationPolicy.Validate(stream, "image/svg+xml", stream.Length);

        // Assert
        result.IsFailure.Should().BeTrue(
            "SVG com <script> deve ser rejeitado antes do armazenamento (DD-007, TASK-21).");
        result.ErrorCode.Should().Be("TA-ERR-013");
    }

    [Fact(DisplayName = "SVG com handler onload= é rejeitado pela AssetValidationPolicy")]
    public void SvgWithEventHandler_IsRejected()
    {
        // Arrange
        const string maliciousSvg =
            """
            <svg xmlns="http://www.w3.org/2000/svg" onload="alert(1)" width="100" height="100">
              <rect width="100" height="100" fill="#fff"/>
            </svg>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(maliciousSvg));

        // Act
        var result = AssetValidationPolicy.Validate(stream, "image/svg+xml", stream.Length);

        // Assert
        result.IsFailure.Should().BeTrue(
            "SVG com handler on* deve ser rejeitado antes do armazenamento (DD-007).");
        result.ErrorCode.Should().Be("TA-ERR-013");
    }

    [Fact(DisplayName = "SVG com xlink:href externo é rejeitado pela AssetValidationPolicy")]
    public void SvgWithExternalXlinkHref_IsRejected()
    {
        // Arrange
        const string maliciousSvg =
            """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
              <image xlink:href="https://evil.com/track.jpg" width="100" height="100"/>
            </svg>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(maliciousSvg));

        // Act
        var result = AssetValidationPolicy.Validate(stream, "image/svg+xml", stream.Length);

        // Assert
        result.IsFailure.Should().BeTrue(
            "SVG com xlink:href externo deve ser rejeitado (DD-007, TASK-21).");
        result.ErrorCode.Should().Be("TA-ERR-013");
    }

    [Fact(DisplayName = "SVG limpo é aceito pela AssetValidationPolicy")]
    public void CleanSvg_IsAccepted()
    {
        // Arrange
        const string cleanSvg =
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <rect width="100" height="100" fill="#1A73E8" rx="8"/>
            </svg>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(cleanSvg));

        // Act
        var result = AssetValidationPolicy.Validate(stream, "image/svg+xml", stream.Length);

        // Assert
        result.IsSuccess.Should().BeTrue(
            "SVG sem conteúdo perigoso deve ser aceito.");
    }
}
