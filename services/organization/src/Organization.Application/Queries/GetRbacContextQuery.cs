using MediatR;
using Organization.Application.Abstractions;
using Organization.Application.Ports;

namespace Organization.Application.Queries;

/// <summary>
/// Query para obter o contexto de RBAC de um usuário (mapa papel por BU).
/// Implementa lógica cache-aside: hit no Redis; miss hidrata do banco e escreve no cache.
/// Redis indisponível degrada para banco com deny-by-default.
/// Resposta nunca inclui PII (Req 13.3, RNF 3.3).
/// </summary>
/// <param name="UserId">Identificador do usuário.</param>
[RequiresRole(AllowAnonymous = true)]
public sealed record GetRbacContextQuery(Guid UserId) : IQuery<RbacContextResult>;

/// <summary>
/// Resultado sem PII — apenas papéis por BU.
/// </summary>
/// <param name="RolesByBu">Mapa de BU → papel para o usuário.</param>
/// <param name="Email">Sempre <c>null</c> — PII não exposta no contexto RBAC.</param>
/// <param name="DisplayName">Sempre <c>null</c> — PII não exposta no contexto RBAC.</param>
public sealed record RbacContextResult(
    IReadOnlyDictionary<Guid, string> RolesByBu,
    string? Email = null,
    string? DisplayName = null);

/// <summary>
/// Handler para <see cref="GetRbacContextQuery"/>.
/// Usa <see cref="MembershipCacheProjector"/> para lógica cache-aside.
/// </summary>
public sealed class GetRbacContextQueryHandler : IRequestHandler<GetRbacContextQuery, RbacContextResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IMembershipCache _cache;
    private readonly IClock _clock;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler.</summary>
    public GetRbacContextQueryHandler(
        IUserRepository userRepository,
        IMembershipCache cache,
        IClock clock,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _cache = cache;
        _clock = clock;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    public async Task<RbacContextResult> Handle(
        GetRbacContextQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        // Cache-aside: tenta o Redis primeiro
        var cached = await _cache.GetAsync(tenantId, request.UserId, cancellationToken);
        if (cached is not null)
            return new RbacContextResult(cached.RolesByBu);

        // Cache miss: hidrata do banco
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            // Deny-by-default: usuário não encontrado → mapa vazio, sem PII
            return new RbacContextResult(new Dictionary<Guid, string>());
        }

        var rolesByBu = user.Memberships.ToDictionary(
            m => m.BuId,
            m => m.Role.Value);

        // Escreve no cache (falha silenciosa por contrato da interface)
        var entry = new MembershipCacheEntry(rolesByBu, _clock.UtcNow);
        await _cache.SetAsync(tenantId, request.UserId, entry, cancellationToken);

        // PII nunca exposta no resultado RBAC
        return new RbacContextResult(rolesByBu);
    }
}
