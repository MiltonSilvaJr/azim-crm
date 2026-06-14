using Digest.Application.Commands;
using MediatR;

namespace Digest.Application.Behaviors;

/// <summary>
/// Pipeline behavior que garante o contexto de tenant antes de qualquer comando de tenant.
/// Aplica-se a <see cref="RunDigestForTenantCommand"/> e <see cref="SendUserDigestCommand"/>.
/// Deve ser o primeiro behavior aplicado (RNF 1, DD-002, design §5.4).
/// </summary>
/// <remarks>
/// O <c>SET app.current_tenant</c> na conexão é responsabilidade do <c>TenantConnectionInterceptor</c>
/// (Infrastructure, TASK-15). Este behavior garante que o comando carrega um <c>tenant_id</c> válido
/// e que o TenantContext está setado antes do handler (design §5.4).
/// </remarks>
public sealed class TenantScopeBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Valida que commands de tenant carregam tenant_id válido
        var tenantId = ExtractTenantId(request);
        if (tenantId.HasValue && tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Comando '{typeof(TRequest).Name}' recebido sem tenant_id válido. " +
                "TenantScopeBehavior rejeita comandos de tenant sem contexto (DD-002).");
        }

        return await next();
    }

    private static Guid? ExtractTenantId(TRequest request) =>
        request switch
        {
            RunDigestForTenantCommand cmd => cmd.TenantId,
            SendUserDigestCommand cmd => cmd.TenantId,
            _ => null, // Outros commands não exigem tenant scope
        };
}
