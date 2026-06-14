namespace Reporting.Domain.Enums;

/// <summary>
/// Papéis de acesso ao módulo reporting.
///
/// O escopo de dados visível a cada papel é determinado por <see cref="Reporting.Domain.ValueObjects.ReportScope"/>
/// e traduzido em predicado SQL pela <c>RbacScopeSpecification</c> (Application, DD-006).
///
/// Regras de acesso (design §4.6, §10):
/// <list type="bullet">
///   <item><description>Vendedor → apenas suas próprias oportunidades (<c>owner_id = :sub</c>).</description></item>
///   <item><description>GestorBU → BUs de membership (<c>bu_id IN :allowed_bus</c>).</description></item>
///   <item><description>TenantAdmin → todo o tenant (sem predicado adicional).</description></item>
///   <item><description>PlatformOperator → negação dura antes do banco (RNF 5).</description></item>
/// </list>
///
/// Mapeia: TASK-04, design §4.6, DD-006, RNF 5.
/// </summary>
public enum ReportingRole
{
    /// <summary>Vendedor: acesso restrito às próprias oportunidades.</summary>
    Vendedor,

    /// <summary>Gestor de BU: acesso restrito às BUs de membership.</summary>
    GestorBU,

    /// <summary>Tenant Admin: acesso a todo o tenant.</summary>
    TenantAdmin,

    /// <summary>
    /// Platform Operator: acesso negado por design (negação dura antes do banco).
    /// A presença deste papel em <c>ReportScope</c> lança <see cref="UnauthorizedAccessException"/>.
    /// Mapeia: RNF 5, DD-006.
    /// </summary>
    PlatformOperator
}
