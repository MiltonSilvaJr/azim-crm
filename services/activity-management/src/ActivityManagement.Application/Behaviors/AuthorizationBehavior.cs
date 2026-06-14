namespace ActivityManagement.Application.Behaviors;

using ActivityManagement.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// Exceção lançada pelo <c>AuthorizationBehavior</c> quando o papel ou escopo de BU
/// do usuário é insuficiente para a operação solicitada (ACT-ERR-007, Req 13).
/// </summary>
public sealed class InsufficientScopeException : Exception
{
    /// <summary>Papel do usuário que tentou a operação.</summary>
    public string UserRole { get; }

    /// <summary>
    /// Inicializa a exceção com o papel do usuário que causou a rejeição.
    /// </summary>
    public InsufficientScopeException(string userRole)
        : base($"Papel '{userRole}' não tem permissão para esta operação.")
    {
        UserRole = userRole;
    }
}

/// <summary>
/// Marcador para Commands e Queries que exigem papel mínimo de escrita (seller ou bu_manager).
/// O <c>AuthorizationBehavior</c> verifica o papel do <see cref="TenantContext"/> antes do handler.
/// Mapeia: Req 13, design §5.4, ACT-ERR-007.
/// </summary>
public interface IRequireWriteRole
{
}

/// <summary>
/// Pipeline behavior MediatR (4ª posição — design §5.4).
/// Aplica RBAC via papel do <see cref="TenantContext"/>:
///   - <c>seller</c>: acesso apenas às próprias atividades.
///   - <c>bu_manager</c>: acesso às atividades da BU.
///   - <c>viewer</c>: somente leitura (rejeita Commands marcados com <see cref="IRequireWriteRole"/>).
/// Retorna <c>InsufficientScopeException</c> (ACT-ERR-007) quando papel insuficiente.
/// Mapeia: Req 13, design §5.4, TASK-06.
/// </summary>
/// <typeparam name="TRequest">Tipo do Command ou Query MediatR.</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    private static readonly HashSet<string> WriteRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "seller",
        "bu_manager",
    };

    /// <summary>
    /// Inicializa o behavior com logger para registro de rejeições.
    /// </summary>
    public AuthorizationBehavior(ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 cancellationToken)
    {
        // Verifica autorização somente quando request exige papel de escrita
        if (request is IRequireWriteRole)
        {
            if (request is ITenantRequest { TenantContext: not null } tenantRequest)
            {
                var role = tenantRequest.TenantContext.Role;
                if (!WriteRoles.Contains(role))
                {
                    _logger.LogWarning(
                        "Acesso negado (ACT-ERR-007): papel '{Role}' insuficiente para {RequestType}.",
                        role,
                        typeof(TRequest).Name);

                    throw new InsufficientScopeException(role);
                }
            }
        }

        return await next(cancellationToken);
    }
}
