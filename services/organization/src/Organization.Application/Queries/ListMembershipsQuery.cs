using MediatR;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Queries;

/// <summary>
/// Query para listar os memberships de um usuário específico (papéis por BU).
/// Autorizado para TAdmin (Req 5).
/// </summary>
/// <param name="UserId">Identificador do usuário.</param>
[RequiresRole("TAdmin")]
public sealed record ListMembershipsQuery(Guid UserId) : IQuery<IReadOnlyList<MembershipResult>>;

/// <summary>Projeção de um membership.</summary>
public sealed record MembershipResult(Guid BuId, string Role);

/// <summary>Handler para <see cref="ListMembershipsQuery"/>.</summary>
public sealed class ListMembershipsQueryHandler : IRequestHandler<ListMembershipsQuery, IReadOnlyList<MembershipResult>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public ListMembershipsQueryHandler(IUserRepository userRepository, ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MembershipResult>> Handle(
        ListMembershipsQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuário '{request.UserId}' não encontrado.");

        return user.Memberships
            .Select(m => new MembershipResult(m.BuId, m.Role.Value))
            .ToList()
            .AsReadOnly();
    }
}
