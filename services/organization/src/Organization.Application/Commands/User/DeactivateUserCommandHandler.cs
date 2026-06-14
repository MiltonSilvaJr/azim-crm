using MediatR;
using Organization.Application.Policies;
using Organization.Application.Ports;
using Organization.Domain.ValueObjects;

namespace Organization.Application.Commands.User;

/// <summary>
/// Handler para <see cref="DeactivateUserCommand"/>.
/// Aplica guards em sequência antes de efetuar o soft-delete:
/// 1. <c>LastTenantAdminPolicy</c> (ORG-ERR-009) — rejeita desativação do último TAdmin.
/// 2. <c>FutureActivitiesSpec</c> (ORG-ERR-011) — rejeita se houver atividades futuras.
/// Invalida cache de RBAC e enfileira <c>UserDeactivated</c> via Outbox.
/// </summary>
public sealed class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantAdminCounter _adminCounter;
    private readonly IActivityCounter _activityCounter;
    private readonly IMembershipCache _cache;
    private readonly IEventOutbox _outbox;
    private readonly IClock _clock;
    private readonly ITenantContext _tenantContext;
    private readonly IOrganizationMetrics _metrics;
    private readonly ILastTenantAdminAlertService _lastAdminAlert;

    /// <summary>Inicializa o handler.</summary>
    public DeactivateUserCommandHandler(
        IUserRepository userRepository,
        ITenantAdminCounter adminCounter,
        IActivityCounter activityCounter,
        IMembershipCache cache,
        IEventOutbox outbox,
        IClock clock,
        ITenantContext tenantContext,
        IOrganizationMetrics metrics,
        ILastTenantAdminAlertService lastAdminAlert)
    {
        _userRepository = userRepository;
        _adminCounter = adminCounter;
        _activityCounter = activityCounter;
        _cache = cache;
        _outbox = outbox;
        _clock = clock;
        _tenantContext = tenantContext;
        _metrics = metrics;
        _lastAdminAlert = lastAdminAlert;
    }

    /// <inheritdoc/>
    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var correlationId = _tenantContext.CorrelationId;

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuário '{request.UserId}' não encontrado.");

        // Guard 1 — LastTenantAdminPolicy (ORG-ERR-009)
        var isTAdmin = user.Memberships.Any(m => m.Role == Role.TAdmin);

        // Alerta operacional: detecta quando o tenant está com apenas 1 TAdmin ativo (RNF 6.3)
        if (isTAdmin)
        {
            var currentAdminCount = await _adminCounter.CountActiveTenantAdminsAsync(tenantId, cancellationToken);
            if (currentAdminCount == 1)
            {
                // Estado crítico: único TAdmin alvo de desativação — política vai rejeitar abaixo,
                // mas emitimos o alerta antes para fins de observabilidade (design §11)
                await _lastAdminAlert.AlertLastAdminAsync(tenantId, currentAdminCount, cancellationToken);
            }
        }

        await LastTenantAdminPolicy.EnforceAsync(
            _adminCounter,
            tenantId,
            request.UserId,
            affectedUserIsTAdmin: isTAdmin,
            cancellationToken);

        // Guard 2 — FutureActivitiesSpec (ORG-ERR-011)
        var futureActivities = await _activityCounter.CountFutureAsync(tenantId, request.UserId, cancellationToken);
        if (futureActivities > 0)
            throw new InvalidOperationException(
                $"Não é possível desativar o usuário '{request.UserId}': há {futureActivities} atividade(s) futura(s) vinculada(s). ORG-ERR-011");

        // Soft-delete
        user.Deactivate(_clock.UtcNow);

        // Persiste + publica evento atomicamente via Outbox
        await _userRepository.SaveAsync(user, cancellationToken);

        foreach (var domainEvent in user.DomainEvents)
            await _outbox.EnqueueAsync(domainEvent, tenantId, correlationId, cancellationToken);

        user.ClearDomainEvents();

        // Invalida cache de RBAC
        await _cache.InvalidateAsync(tenantId, request.UserId, cancellationToken);

        _metrics.IncrementUsersDeactivated();
    }
}
