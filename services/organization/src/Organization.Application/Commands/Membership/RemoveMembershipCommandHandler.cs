using MediatR;
using Organization.Application.Policies;
using Organization.Application.Ports;
using Organization.Domain.ValueObjects;

namespace Organization.Application.Commands.Membership;

/// <summary>
/// Handler para <see cref="RemoveMembershipCommand"/>.
/// Aplica <c>LastTenantAdminPolicy</c> antes de remover membership de TAdmin.
/// Invalida cache de RBAC após remoção.
/// </summary>
public sealed class RemoveMembershipCommandHandler : IRequestHandler<RemoveMembershipCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantAdminCounter _adminCounter;
    private readonly IMembershipCache _cache;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public RemoveMembershipCommandHandler(
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
    public async Task Handle(RemoveMembershipCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuário '{request.UserId}' não encontrado.");

        // Verifica se o membership a remover é de TAdmin
        var membership = user.Memberships.FirstOrDefault(m => m.BuId == request.BuId);
        var isTAdmin = membership?.Role == Role.TAdmin;

        // Aplica LastTenantAdminPolicy (ORG-ERR-009)
        await LastTenantAdminPolicy.EnforceAsync(
            _adminCounter,
            tenantId,
            request.UserId,
            affectedUserIsTAdmin: isTAdmin,
            cancellationToken);

        user.RemoveMembership(request.BuId);

        await _userRepository.SaveAsync(user, cancellationToken);
        await _cache.InvalidateAsync(tenantId, request.UserId, cancellationToken);
    }
}
