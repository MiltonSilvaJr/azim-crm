using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Specifications;

/// <summary>
/// Traduz um <see cref="ReportScope"/> no predicado SQL adicional de escopo RBAC.
///
/// O predicado é aplicado APÓS o isolamento de tenant (RLS + Global Query Filter).
/// Cada papel gera um predicado diferente conforme o design §4.6 e DD-006:
/// <list type="bullet">
///   <item><description>Vendedor → <c>owner_id = @ownerId</c></description></item>
///   <item><description>GestorBU → <c>bu_id IN @allowedBuIds</c></description></item>
///   <item><description>TenantAdmin → predicado vazio (sem restrição adicional)</description></item>
///   <item><description>PlatformOperator → nunca chega aqui; <see cref="ReportScope.Create"/> lança antes</description></item>
/// </list>
///
/// O escopo de RBAC é separado da RLS de tenant (DD-006):
/// a view permanece reutilizável; a restrição de papel é aplicada na query do handler.
///
/// Mapeia: TASK-04, design §4.6, DD-006, Req 7, RNF 5.
/// </summary>
public sealed class RbacScopeSpecification
{
    /// <summary>
    /// Constrói o predicado SQL parametrizado para o escopo fornecido.
    /// </summary>
    /// <param name="scope">Escopo resolvido no servidor.</param>
    /// <returns><see cref="SqlPredicate"/> com a cláusula e os parâmetros.</returns>
    /// <exception cref="InvalidOperationException">
    ///   Se o papel não for mapeável (nunca deve ocorrer, pois PlatformOperator é bloqueado na construção do scope).
    /// </exception>
    public SqlPredicate BuildPredicate(ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return scope.Role switch
        {
            ReportingRole.Vendedor => BuildVendedorPredicate(scope),
            ReportingRole.GestorBU => BuildGestorBuPredicate(scope),
            ReportingRole.TenantAdmin => SqlPredicate.Empty,

            // PlatformOperator nunca chega aqui: ReportScope.Create lança UnauthorizedAccessException.
            // Caso chegue por bug, falha fechada.
            _ => throw new InvalidOperationException(
                $"Papel não mapeável em predicado de escopo: {scope.Role}. (DD-006, design §4.6)")
        };
    }

    private static SqlPredicate BuildVendedorPredicate(ReportScope scope)
    {
        var ownerId = scope.OwnerRestrictedTo
            ?? throw new InvalidOperationException(
                "Vendedor deve ter OwnerRestrictedTo preenchido. (DD-006)");

        return SqlPredicate.Create(
            clause: "owner_id = @ownerId",
            parameters: new Dictionary<string, object?> { ["ownerId"] = ownerId });
    }

    private static SqlPredicate BuildGestorBuPredicate(ReportScope scope) =>
        SqlPredicate.Create(
            clause: "bu_id IN @allowedBuIds",
            parameters: new Dictionary<string, object?> { ["allowedBuIds"] = scope.AllowedBuIds.ToList() });
}
