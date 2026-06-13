using MediatR;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Queries;

/// <summary>
/// Query para listar os usuários ativos do tenant (paginada).
/// PII exposta somente para TAdmin (Req 7, RNF 3.3).
/// </summary>
/// <param name="Page">Número da página (1-based).</param>
/// <param name="PageSize">Tamanho da página.</param>
[RequiresRole("TAdmin")]
public sealed record ListUsersQuery(int Page, int PageSize) : IQuery<IReadOnlyList<UserResult>>;

/// <summary>Projeção de um usuário com memberships.</summary>
/// <param name="Id">Identificador do usuário.</param>
/// <param name="Email">E-mail (PII — exposto apenas para TAdmin).</param>
/// <param name="Memberships">Papéis por BU.</param>
public sealed record UserResult(
    Guid Id,
    string Email,
    IReadOnlyList<UserMembershipResult> Memberships);

/// <summary>Projeção de um membership de usuário.</summary>
public sealed record UserMembershipResult(Guid BuId, string Role);

/// <summary>Handler para <see cref="ListUsersQuery"/>.</summary>
public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, IReadOnlyList<UserResult>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public ListUsersQueryHandler(IUserRepository userRepository, ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserResult>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await _userRepository.ListActiveAsync(request.Page, request.PageSize, cancellationToken);

        return users
            .Select(u => new UserResult(
                u.Id,
                u.Email,
                u.Memberships
                    .Select(m => new UserMembershipResult(m.BuId, m.Role.Value))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly();
    }
}
