using Microsoft.AspNetCore.Http;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Api.Tests.Infrastructure;

/// <summary>
/// Implementação de <see cref="ICurrentUserContext"/> para testes de integração de API.
/// Lê o papel diretamente do header <c>X-Test-Role</c> via <see cref="IHttpContextAccessor"/>,
/// eliminando dependência do pipeline de autenticação JWT em testes.
/// </summary>
internal sealed class TestCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserContext
{
    private string Role =>
        httpContextAccessor.HttpContext?.Request.Headers["X-Test-Role"].FirstOrDefault()
        ?? string.Empty;

    /// <inheritdoc/>
    public string UserId =>
        httpContextAccessor.HttpContext?.Request.Headers["X-Test-UserId"].FirstOrDefault()
        ?? "test-user-id";

    /// <inheritdoc/>
    public bool IsPlatformOperator => Role == "PlatformOperator";

    /// <inheritdoc/>
    public bool IsTenantAdmin => Role == "TenantAdmin";

    /// <inheritdoc/>
    public bool IsViewer => Role is "Viewer" or "TenantAdmin";
}
