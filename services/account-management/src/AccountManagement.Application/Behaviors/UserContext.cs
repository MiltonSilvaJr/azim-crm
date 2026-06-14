namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Contexto do usuário autenticado, contendo identidade e papel (role).
///
/// Injetado como serviço scoped. Populado pelo middleware de autenticação
/// antes do pipeline MediatR (design §5.4, rule jwt-permissions.md).
///
/// Mapeia: design §5.4, design §10, Req 9, RNF 6.
/// </summary>
public sealed class UserContext
{
    private Guid? _userId;
    private string? _role;

    /// <summary>Identificador do usuário autenticado.</summary>
    public Guid? UserId => _userId;

    /// <summary>Papel (role) do usuário no sistema (ex.: Viewer, Vendedor, TenantAdmin).</summary>
    public string? Role => _role;

    /// <summary>
    /// Define a identidade e o papel do usuário. Chamado pelo middleware de autenticação.
    /// </summary>
    public void SetUser(Guid userId, string role)
    {
        _userId = userId;
        _role = role;
    }

    /// <summary>
    /// Retorna o <see cref="UserId"/> ou lança <see cref="InvalidOperationException"/>
    /// quando o contexto não foi inicializado.
    /// </summary>
    public Guid GetRequiredUserId() =>
        _userId ?? throw new InvalidOperationException(
            "O contexto de usuário não foi inicializado.");
}
