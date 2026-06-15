namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Contexto de escopo de Business Unit resolvido pelo middleware de autenticação
/// a partir dos claims do principal (memberships). Análogo a <see cref="TenantContext"/>.
///
/// Carrega o conjunto de BU ids do usuário e o flag <see cref="IsTenantWide"/>.
/// Populado antes de qualquer operação de dados para garantir o isolamento por BU
/// em profundidade (ADR-0009).
///
/// Papéis de gestão do tenant (TenantAdmin, Gestor) recebem <see cref="IsTenantWide"/> = true
/// e enxergam contas de todas as BUs do tenant. Membros enxergam apenas as BUs em que possuem
/// membership (ou grants adicionais configurados pelo TenantAdmin).
///
/// Mapeia: ADR-0009, VAL-ACC-03, design §5.4.
/// </summary>
public sealed class BuScopeContext
{
    private IReadOnlyCollection<Guid>? _buIds;
    private bool? _isTenantWide;

    /// <summary>
    /// Conjunto de BU ids do usuário autenticado.
    /// Nulo antes de <see cref="SetScope"/> ser chamado.
    /// </summary>
    public IReadOnlyCollection<Guid>? BuIds => _buIds;

    /// <summary>
    /// Indica se o usuário possui visão tenant-wide (papel de gestão).
    /// Quando <c>true</c>, o filtro de BU é dispensado (dentro do tenant — ADR-0001 preservado).
    /// Nulo antes de <see cref="SetScope"/> ser chamado.
    /// </summary>
    public bool? IsTenantWide => _isTenantWide;

    /// <summary>
    /// Define o escopo de BU do usuário. Chamado pelo middleware de autenticação
    /// ou por <see cref="BuScopeBehavior{TRequest,TResponse}"/> a partir dos claims.
    /// </summary>
    /// <param name="buIds">Conjunto de BU ids do usuário.</param>
    /// <param name="isTenantWide">
    /// <c>true</c> para papéis de gestão (TenantAdmin, Gestor) — bypass do filtro de BU.
    /// </param>
    public void SetScope(IReadOnlyCollection<Guid> buIds, bool isTenantWide)
    {
        _buIds = buIds;
        _isTenantWide = isTenantWide;
    }

    /// <summary>
    /// Retorna o conjunto de BU ids ou lança <see cref="InvalidOperationException"/>
    /// quando o contexto não foi inicializado.
    /// </summary>
    public IReadOnlyCollection<Guid> GetRequiredBuIds() =>
        _buIds ?? throw new InvalidOperationException(
            "O contexto de escopo de BU não foi inicializado. " +
            "O BuScopeBehavior deve ser executado antes do handler.");

    /// <summary>
    /// Retorna o flag tenant-wide ou lança <see cref="InvalidOperationException"/>
    /// quando o contexto não foi inicializado.
    /// </summary>
    public bool GetRequiredIsTenantWide() =>
        _isTenantWide ?? throw new InvalidOperationException(
            "O contexto de escopo de BU não foi inicializado. " +
            "O BuScopeBehavior deve ser executado antes do handler.");

    /// <summary>
    /// Indica se o contexto foi devidamente inicializado.
    /// </summary>
    public bool IsInitialized => _buIds is not null && _isTenantWide.HasValue;
}
