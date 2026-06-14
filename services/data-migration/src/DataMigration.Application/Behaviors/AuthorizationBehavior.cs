using DataMigration.Application.Commands.Import;
using DataMigration.Application.Commands.Upload;
using DataMigration.Application.Commands.DryRun;
using DataMigration.Application.Commands.Triage;
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
///     PlatformOperator também pode realizar triagem.
///   - Ambos: queries de status/report.
///
/// Lança MIG-ERR-009 (403 Forbidden) quando o papel não tem permissão.
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
        var policy = GetPolicy(typeof(TRequest));

        if (policy is not null && !policy.IsAuthorized(role))
        {
            throw new MigrationDomainException(
                "MIG-ERR-009",
                $"Acesso negado: papel '{role ?? "(nenhum)"}' não tem permissão para " +
                $"'{typeof(TRequest).Name}'. Papel mínimo: '{policy.MinimumRole}'.");
        }

        return await next();
    }

    /// <summary>
    /// Retorna a política de autorização para o tipo de request.
    /// Retorna <c>null</c> para requests sem restrição de papel (queries públicas).
    /// </summary>
    private static RbacPolicy? GetPolicy(Type requestType)
    {
        // Operações exclusivas de PlatformOperator (design §10)
        if (requestType == typeof(UploadSpreadsheetCommand)
            || requestType == typeof(RunDryRunCommand)
            || requestType == typeof(ExecuteImportCommand))
        {
            return RbacPolicy.RequirePlatformOperator;
        }

        // Operações de triagem: TenantAdmin ou PlatformOperator (design §10)
        if (requestType == typeof(AssignOwnerCommand)
            || requestType == typeof(BulkAssignOwnerCommand)
            || requestType == typeof(MarkReadyToImportCommand)
            || requestType == typeof(ResolveDedupeCommand)
            || requestType == typeof(ResolvePartnerPctCommand)
            || requestType == typeof(ResolveStagePendingCommand)
            || requestType == typeof(SaveTriageProgressCommand))
        {
            return RbacPolicy.RequireTenantAdminOrPlatformOperator;
        }

        // Queries sem restrição de papel (ambos os papéis podem consultar)
        return null;
    }

    // =========================================================================
    // Política de autorização interna
    // =========================================================================

    private sealed record RbacPolicy(string MinimumRole, Func<string?, bool> IsAuthorized)
    {
        public static readonly RbacPolicy RequirePlatformOperator = new(
            "PlatformOperator",
            role => string.Equals(role, "PlatformOperator", StringComparison.OrdinalIgnoreCase));

        public static readonly RbacPolicy RequireTenantAdminOrPlatformOperator = new(
            "TenantAdmin",
            role => string.Equals(role, "TenantAdmin", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(role, "PlatformOperator", StringComparison.OrdinalIgnoreCase));
    }
}
