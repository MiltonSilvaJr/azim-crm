using DataMigration.Application.Commands.Import;
using DataMigration.Application.Commands.Upload;
using DataMigration.Application.Commands.DryRun;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using MediatR;

namespace DataMigration.Application.Behaviors;

/// <summary>
/// Pipeline behavior de autorização baseada em RBAC.
///
/// Papéis (design §10):
///   - <b>PlatformOperator</b>: upload, dry-run, execute.
///   - <b>TenantAdmin</b>: triagem (owners, estágios, parceiros, dedupe, ready).
///   - Ambos: queries de status/report.
///
/// Rastreia: design §5.4, §10, ADR-0001, TASK-12.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentTenantContext _tenantContext;

    /// <summary>Cria o behavior com o contexto de tenant injetado.</summary>
    public AuthorizationBehavior(ICurrentTenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var role = _tenantContext.Role;
        var requiredRole = GetRequiredRole(typeof(TRequest));

        if (requiredRole is not null && !IsAuthorized(role, requiredRole))
        {
            throw new MigrationDomainException(
                "MIG-ERR-005",
                $"Autorização negada: papel '{role}' não tem permissão para '{typeof(TRequest).Name}'. " +
                $"Papel mínimo requerido: '{requiredRole}'.");
        }

        return await next();
    }

    /// <summary>
    /// Define o papel mínimo requerido para cada tipo de command.
    /// Retorna null para comandos sem restrição de papel.
    /// </summary>
    private static string? GetRequiredRole(Type requestType)
    {
        if (requestType == typeof(UploadSpreadsheetCommand)
            || requestType == typeof(RunDryRunCommand)
            || requestType == typeof(ExecuteImportCommand))
        {
            return "PlatformOperator";
        }

        // Triagem: TenantAdmin (também aceita PlatformOperator)
        return null;
    }

    private static bool IsAuthorized(string? role, string requiredRole)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        // PlatformOperator pode fazer tudo; TenantAdmin pode fazer apenas triagem
        if (requiredRole == "PlatformOperator")
        {
            return string.Equals(role, "PlatformOperator", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(role, requiredRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "PlatformOperator", StringComparison.OrdinalIgnoreCase);
    }
}
