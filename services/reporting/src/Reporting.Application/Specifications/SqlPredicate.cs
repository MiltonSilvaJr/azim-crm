namespace Reporting.Application.Specifications;

/// <summary>
/// Predicado SQL parametrizado gerado pela <see cref="RbacScopeSpecification"/>.
///
/// Representa a cláusula <c>WHERE</c> adicional de escopo RBAC (além do tenant isolado por RLS).
/// Usado pela camada de infraestrutura (Dapper) para adicionar o filtro correto à query.
///
/// Mapeia: TASK-04, design §4.6, DD-006, Req 7.
/// </summary>
public sealed class SqlPredicate
{
    /// <summary>Predicado vazio — sem restrição de escopo adicional (TenantAdmin).</summary>
    public static readonly SqlPredicate Empty = new(string.Empty, new Dictionary<string, object?>());

    /// <summary>
    /// Cláusula SQL do predicado (ex: <c>"owner_id = @ownerId"</c>, <c>"bu_id IN @allowedBuIds"</c>).
    /// Vazio para TenantAdmin.
    /// </summary>
    public string Clause { get; }

    /// <summary>
    /// Parâmetros nomeados do predicado, prontos para uso com Dapper.
    /// Vazio para TenantAdmin.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>Retorna <c>true</c> se o predicado não adiciona restrição ao scope.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Clause);

    private SqlPredicate(string clause, Dictionary<string, object?> parameters)
    {
        Clause     = clause;
        Parameters = parameters;
    }

    /// <summary>
    /// Cria um predicado com cláusula e parâmetros.
    /// </summary>
    public static SqlPredicate Create(string clause, Dictionary<string, object?> parameters) =>
        new(clause, parameters);
}
