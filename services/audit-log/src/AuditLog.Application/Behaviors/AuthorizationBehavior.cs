using AuditLog.Application.Abstractions;
using AuditLog.Application.Errors;
using AuditLog.Application.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuditLog.Application.Behaviors;

/// <summary>
/// Behavior de pipeline MediatR que verifica autorização RBAC para queries de auditoria (design §5.4, REQ-008).
/// Posição na pipeline: 3ª — depois de <see cref="TenantContextBehavior{TRequest,TResponse}"/>.
/// <para>
/// <b>Aplica-se apenas a requisições que implementam <see cref="IAuditQuery"/>.</b>
/// O <c>RecordAuditEntryCommand</c> é uma porta interna confiável e <b>não</b> passa por este behavior (design §5.4).
/// </para>
/// <para>
/// Papéis com acesso: <see cref="AuditRoles.TenantAdmin"/>, <see cref="AuditRoles.GestorBU"/>.
/// Demais papéis recebem <see cref="AuditAuthorizationException"/> com código <see cref="AuditErrorCodes.AccessDenied"/>.
/// </para>
/// </summary>
/// <typeparam name="TRequest">Tipo da requisição MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta MediatR.</typeparam>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly IReadOnlySet<string> AllowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AuditRoles.TenantAdmin,
        AuditRoles.GestorBU
    };

    private readonly IUserContext _userContext;
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    /// <summary>Inicializa o behavior com suas dependências.</summary>
    public AuthorizationBehavior(
        IUserContext userContext,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(logger);

        _userContext = userContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Aplica apenas a queries de auditoria (IAuditQuery); commands passam direto.
        if (request is not IAuditQuery)
            return await next(cancellationToken);

        var role = _userContext.Role;

        if (role is null || !AllowedRoles.Contains(role))
        {
            _logger.LogWarning(
                "Acesso negado à query de auditoria {RequestType}. Papel={Role}",
                typeof(TRequest).Name,
                role ?? "(ausente)");

            throw new AuditAuthorizationException(
                AuditErrorCodes.AccessDenied,
                "Acesso negado à trilha de auditoria. Papel insuficiente para esta operação.");
        }

        return await next(cancellationToken);
    }
}
