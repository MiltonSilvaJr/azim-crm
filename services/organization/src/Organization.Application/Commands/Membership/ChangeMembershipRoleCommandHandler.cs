using MediatR;
using Organization.Application.Policies;
using Organization.Application.Ports;
using Organization.Domain.ValueObjects;

namespace Organization.Application.Commands.Membership;

/// <summary>
/// Handler para <see cref="ChangeMembershipRoleCommand"/>.
/// Aplica <c>LastTenantAdminPolicy</c> antes de rebaixar um TAdmin.
/// Invalida cache de RBAC após alteração.
/// </summary>
public sealed class ChangeMembershipRoleCommandHandler : IRequestHandler<ChangeMembershipRoleCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantAdminCounter _adminCounter;
    private readonly IMembershipCache _cache;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public ChangeMembershipRoleCommandHandler(
        IUserRepository userRepository,
        ITenantAdminCounter adminCounter,
        IMembershipCache cache,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _adminCounter = adminCounter;
        _cache = cache;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(ChangeMembershipRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuário '{request.UserId}' não encontrado.");

        // Verifica se o membership atual é TAdmin (se for rebaixamento, precisa checar a policy)
        var currentMembership = user.Memberships.FirstOrDefault(m => m.BuId == request.BuId);
        var isTAdminBeforeChange = currentMembership?.Role == Role.TAdmin;
        var newRoleIsTAdmin = string.Equals(request.NewRole, "TAdmin", StringComparison.Ordinal);
        var isDowngrade = isTAdminBeforeChange && !newRoleIsTAdmin;

        if (isDowngrade)
        {
            await LastTenantAdminPolicy.EnforceAsync(
                _adminCounter,
                tenantId,
                request.UserId,
                affectedUserIsTAdmin: true,
                cancellationToken);
        }

        var newRole = Role.Create(request.NewRole);
        user.ChangeMembershipRole(request.BuId, newRole);

        await _userRepository.SaveAsync(user, cancellationToken);
        await _cache.InvalidateAsync(tenantId, request.UserId, cancellationToken);
    }
}
