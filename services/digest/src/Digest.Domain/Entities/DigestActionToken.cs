using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Domain.Entities;

/// <summary>
/// Entidade que representa um token de ação de um clique emitido no digest.
/// Apenas o <see cref="TokenHash"/> (SHA-256) é persistido; o token em claro nunca é armazenado (DD-007).
/// <c>expires_at</c> = <c>created_at</c> + 48h (DD-004, ADR-0006).
/// Ciclo de vida: emitido → expirado (purge pós-expiração) | emitido → usado (fora deste worker).
/// </summary>
/// <remarks>
/// O consumo (<c>used_at</c>) é responsabilidade do activity-management (Req 7.4).
/// Sem dependências de EF Core ou infraestrutura.
/// </remarks>
public sealed class DigestActionToken
{
    private const int ExpiryHours = 48;

    /// <summary>Identificador único do token (UUID).</summary>
    public Guid Id { get; }

    /// <summary>Identificador do tenant.</summary>
    public Guid TenantId { get; }

    /// <summary>Identificador do usuário destinatário.</summary>
    public Guid UserId { get; }

    /// <summary>Identificador lógico da atividade vinculada (sem FK física — DD-001).</summary>
    public Guid ActivityId { get; }

    /// <summary>Tipo de ação: <see cref="ActionType.Complete"/> ou <see cref="ActionType.Reschedule"/>.</summary>
    public ActionType Action { get; }

    /// <summary>Hash SHA-256 do token opaco. Único campo relacionado ao token persistido (DD-007).</summary>
    public byte[] TokenHash { get; }

    /// <summary>Data/hora de expiração do token (<c>created_at</c> + 48h — DD-004).</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Data/hora de uso do token. Setado pelo activity-management (fora deste worker — Req 7.4).
    /// <c>null</c> enquanto não utilizado.
    /// </summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Data/hora de criação do token.</summary>
    public DateTimeOffset CreatedAt { get; }

    private DigestActionToken(
        Guid id,
        Guid tenantId,
        Guid userId,
        Guid activityId,
        ActionType action,
        byte[] tokenHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        ActivityId = activityId;
        Action = action;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = createdAt.AddHours(ExpiryHours);
    }

    /// <summary>
    /// Emite um novo <see cref="DigestActionToken"/> a partir de um <see cref="ActionToken"/> VO.
    /// Extrai o hash do VO; o token em claro nunca é armazenado na entidade.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Não pode ser vazio.</param>
    /// <param name="userId">Identificador do usuário. Não pode ser vazio.</param>
    /// <param name="activityId">Identificador da atividade. Não pode ser vazio.</param>
    /// <param name="action">Tipo de ação (<see cref="ActionType.Complete"/> ou <see cref="ActionType.Reschedule"/>).</param>
    /// <param name="token">Token emitido pelo <c>IActionTokenFactory</c>. O hash é extraído aqui.</param>
    /// <exception cref="ArgumentException">Quando algum GUID é vazio ou <paramref name="token"/> é nulo.</exception>
    public static DigestActionToken Issue(
        Guid tenantId,
        Guid userId,
        Guid activityId,
        ActionType action,
        ActionToken token)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));
        if (userId == Guid.Empty)
            throw new ArgumentException("user_id não pode ser vazio.", nameof(userId));
        if (activityId == Guid.Empty)
            throw new ArgumentException("activity_id não pode ser vazio.", nameof(activityId));
        ArgumentNullException.ThrowIfNull(token);

        return new DigestActionToken(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            userId: userId,
            activityId: activityId,
            action: action,
            tokenHash: token.TokenHash,
            createdAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Retorna <c>true</c> se o token já expirou no instante fornecido.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) => now > ExpiresAt;
}
