using Microsoft.AspNetCore.Http;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Api.Tests.Infrastructure;

/// <summary>
/// Implementação de <see cref="ITenantContext"/> para testes de integração de API.
/// Lê o tenant_id diretamente do header <c>X-Test-TenantId</c> via <see cref="IHttpContextAccessor"/>.
/// </summary>
internal sealed class TestTenantContext(IHttpContextAccessor httpContextAccessor)
    : ITenantContext
{
    /// <inheritdoc/>
    public Guid? TenantId
    {
        get
        {
            var raw = httpContextAccessor.HttpContext?.Request.Headers["X-Test-TenantId"].FirstOrDefault();
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    /// <inheritdoc/>
    public string? Slug => null;

    /// <inheritdoc/>
    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? httpContextAccessor.HttpContext?.TraceIdentifier;
}
