using MediatR;
using Organization.Application.Policies;
using Organization.Application.Ports;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;

namespace Organization.Application.Commands.Membership;

/// <summary>
/// Handler para <see cref="AssignMembershipCommand"/>.
/// Cria membership via domínio; invalida cache de RBAC.
/// </summary>
public sealed class AssignMembershipCommandHandler : IRequestHandler<AssignMembershipCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IMembershipCache _cache;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public AssignMembershipCommandHandler(
        IUserRepository userRepository,
        IMembershipCache cache,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _cache = cache;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task Handle(AssignMembershipCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuário '{request.UserId}' não encontrado.");

        var role = Role.Create(request.Role);
        user.AssignMembership(request.BuId, role, Guid.NewGuid());

        await _userRepository.SaveAsync(user, cancellationToken);

        // Invalida cache de RBAC após alteração de membership
        await _cache.InvalidateAsync(_tenantContext.TenantId, request.UserId, cancellationToken);
    }
}
