using Reporting.Domain.Enums;

namespace Reporting.Domain.ValueObjects;

/// <summary>
/// Objeto de valor imutável que representa o escopo de acesso resolvido no servidor
/// a partir do token JWT e dos memberships do usuário.
///
/// <b>Nunca construído a partir de entrada do cliente.</b>
/// Deve ser criado exclusivamente pelo <c>IScopeResolver</c> no servidor (Req 7, DD-006).
///
/// Invariantes:
/// <list type="bullet">
///   <item><description><see cref="TenantId"/> não pode ser <see cref="Guid.Empty"/>.</description></item>
///   <item><description><see cref="AllowedBuIds"/> não pode ser vazio para <see cref="ReportingRole.GestorBU"/>.</description></item>
///   <item><description><see cref="ReportingRole.PlatformOperator"/> é negação dura — lança <see cref="UnauthorizedAccessException"/>.</description></item>
/// </list>
///
/// Mapeia: TASK-04, design §4.3, DD-006, Req 7, RNF 5.
/// </summary>
public sealed record ReportScope
{
    /// <summary>Identificador do tenant. Nunca nulo ou vazio.</summary>
    public Guid TenantId { get; }

    /// <summary>Papel do usuário que determina o predicado de escopo.</summary>
    public ReportingRole Role { get; }

    /// <summary>
    /// BUs permitidas para o usuário.
    /// Preenchido apenas para <see cref="ReportingRole.GestorBU"/>;
    /// vazio para <see cref="ReportingRole.TenantAdmin"/> e <see cref="ReportingRole.Vendedor"/>.
    /// </summary>
    public IReadOnlySet<Guid> AllowedBuIds { get; }

    /// <summary>
    /// Restrição ao owner (userId) — preenchida apenas para <see cref="ReportingRole.Vendedor"/>.
    /// </summary>
    public Guid? OwnerRestrictedTo { get; }

    private ReportScope(
        Guid tenantId,
        ReportingRole role,
        IReadOnlySet<Guid> allowedBuIds,
        Guid? ownerRestrictedTo)
    {
        TenantId          = tenantId;
        Role              = role;
        AllowedBuIds      = allowedBuIds;
        OwnerRestrictedTo = ownerRestrictedTo;
    }

    /// <summary>
    /// Cria um <see cref="ReportScope"/> validado.
    /// Deve ser chamado exclusivamente pelo <c>IScopeResolver</c> no servidor.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant (do JWT).</param>
    /// <param name="role">Papel do usuário.</param>
    /// <param name="allowedBuIds">BUs permitidas (obrigatório para GestorBU).</param>
    /// <param name="ownerRestrictedTo">UserId do Vendedor (obrigatório para Vendedor).</param>
    /// <exception cref="ArgumentException">
    ///   Se <paramref name="tenantId"/> for vazio ou <paramref name="allowedBuIds"/> vazio para GestorBU.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    ///   Se o papel for <see cref="ReportingRole.PlatformOperator"/>.
    /// </exception>
    public static ReportScope Create(
        Guid tenantId,
        ReportingRole role,
        IEnumerable<Guid> allowedBuIds,
        Guid? ownerRestrictedTo)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "tenantId não pode ser Guid.Empty. ReportScope deve ser resolvido pelo IScopeResolver com o tenant do token JWT. (Req 7, DD-006)",
                nameof(tenantId));
        }

        if (role == ReportingRole.PlatformOperator)
        {
            throw new UnauthorizedAccessException(
                "PlatformOperator não pode acessar relatórios. Negação dura antes do banco. (RNF 5, DD-006, REPORT-ERR-005)");
        }

        var buSet = allowedBuIds.ToHashSet();

        if (role == ReportingRole.GestorBU && buSet.Count == 0)
        {
            throw new ArgumentException(
                "allowedBuIds não pode ser vazio para o papel GestorBU. O usuário não possui BU de membership. (DD-006)",
                nameof(allowedBuIds));
        }

        // Cópia defensiva para garantir imutabilidade
        return new ReportScope(tenantId, role, buSet.ToHashSet(), ownerRestrictedTo);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"ReportScope(tenant={TenantId}, role={Role}, buCount={AllowedBuIds.Count}, owner={OwnerRestrictedTo?.ToString() ?? "none"})";
}
