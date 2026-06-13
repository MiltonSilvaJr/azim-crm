using System.Security.Claims;
using TenantAdministration.Application.Ports;

namespace TenantAdministration.Api.Infrastructure;

/// <summary>
/// Implementação de <see cref="ITenantContext"/> baseada no <see cref="HttpContext"/> corrente.
/// Extrai o tenant_id do claim JWT ou do header de teste (ambiente de testes).
/// Nulo no plano de plataforma (PlatOp) — ADR-0009.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    : ITenantContext
{
    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User;

    /// <inheritdoc/>
    public Guid? TenantId
    {
        get
        {
            var raw = Principal?.FindFirstValue("tenant_id")
                   ?? httpContextAccessor.HttpContext?.Request.Headers["X-Test-TenantId"].FirstOrDefault();

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    /// <inheritdoc/>
    public string? Slug => null; // Resolvido por middleware em produção; não necessário para API tests

    /// <inheritdoc/>
    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? httpContextAccessor.HttpContext?.TraceIdentifier;
}
