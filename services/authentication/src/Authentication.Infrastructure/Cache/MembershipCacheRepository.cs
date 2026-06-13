using Authentication.Domain.ValueObjects;
using StackExchange.Redis;
using System.Text.Json;

namespace Authentication.Infrastructure.Cache;

/// <summary>
/// Repositório de cache de <see cref="MembershipSet"/> usando Redis/Memorystore (TLS, RNF 6).
///
/// Chave de cache: <c>auth:membership:{tenant_id}:{user_id}</c>
/// A chave inclui <c>tenant_id</c> para garantir isolamento total entre tenants
/// (DD-007, design.md § 14 — nenhuma entrada é compartilhada entre tenants).
///
/// TTL configurável (~5 min por padrão). Invalidação explícita via
/// <see cref="InvalidateAsync"/> ao consumir <c>user.role_changed</c> ou
/// <c>user.deactivated</c> do módulo organization (DD-007, Req 5.5).
///
/// Falha de invalidação degrada para o TTL curto sem comprometer isolamento (DD-007).
///
/// Mapeia: Req 5.5; RNF 2.3, RNF 6; design.md § 6.2, § 6.3; DD-007, TASK-12.
/// </summary>
public sealed class MembershipCacheRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _ttl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Inicializa o repositório com a conexão Redis e o TTL configurável.
    /// </summary>
    /// <param name="redis">Conexão Redis (TLS em produção — RNF 6).</param>
    /// <param name="ttlMinutes">TTL em minutos para entradas de cache (padrão 5 min, DD-007).</param>
    public MembershipCacheRepository(IConnectionMultiplexer redis, int ttlMinutes = 5)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _ttl = TimeSpan.FromMinutes(ttlMinutes > 0 ? ttlMinutes : 5);
    }

    /// <summary>
    /// Armazena o <see cref="MembershipSet"/> do usuário no tenant com TTL configurado.
    ///
    /// Chave: <c>auth:membership:{tenant_id}:{user_id}</c>
    /// </summary>
    public async Task SetAsync(
        Guid tenantId,
        Guid userId,
        MembershipSet membershipSet,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = CacheKey(tenantId, userId);
        var value = Serialize(membershipSet);

        await db.StringSetAsync(key, value, _ttl);
    }

    /// <summary>
    /// Recupera o <see cref="MembershipSet"/> do cache, ou <c>null</c> se não encontrado/expirado.
    /// </summary>
    public async Task<MembershipSet?> GetAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = CacheKey(tenantId, userId);
        var value = await db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return null;

        return Deserialize(value!);
    }

    /// <summary>
    /// Invalida a entrada de cache do usuário no tenant (DEL da chave).
    ///
    /// Chamado ao receber <c>user.role_changed</c> ou <c>user.deactivated</c> do organization.
    /// Operação idempotente: DEL de chave inexistente não causa erro (DD-007, Req 5.5).
    /// </summary>
    public async Task InvalidateAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = CacheKey(tenantId, userId);

        // DEL é idempotente: retorna 0 (não encontrado) sem erro
        await db.KeyDeleteAsync(key);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Chave de cache seguindo o formato obrigatório do design:
    /// <c>auth:membership:{tenant_id}:{user_id}</c>
    ///
    /// A inclusão de <c>tenant_id</c> garante que nenhuma entrada seja
    /// compartilhada entre tenants (design.md § 6.2, DD-007).
    /// </summary>
    private static string CacheKey(Guid tenantId, Guid userId) =>
        $"auth:membership:{tenantId:N}:{userId:N}";

    private static string Serialize(MembershipSet membershipSet)
    {
        var dto = new MembershipSetDto(
            Entries: membershipSet.Entries
                .Select(e => new MembershipEntryDto(e.BuId, e.Role))
                .ToList(),
            CachedAt: membershipSet.CachedAt);

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static MembershipSet Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<MembershipSetDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Falha na deserialização do MembershipSet.");

        var entries = dto.Entries
            .Select(e => new MembershipEntry(e.BuId, e.Role))
            .ToList();

        return MembershipSet.Create(entries, dto.CachedAt);
    }

    // DTO interno para serialização JSON (sem expor tipos de domínio ao serializador)
    private sealed record MembershipSetDto(
        List<MembershipEntryDto> Entries,
        DateTimeOffset CachedAt);

    private sealed record MembershipEntryDto(Guid BuId, string Role);
}
