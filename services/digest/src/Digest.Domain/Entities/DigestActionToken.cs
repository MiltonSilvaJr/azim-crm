using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Domain.Entities;

/// <summary>
/// Entidade que representa um token de ação de um clique emitido no digest.
/// Apenas o <see cref="TokenHash"/> (SHA-256) é persistido; o token em claro nunca é armazenado (DD-007).
/// <c>expires_at</c> = <c>created_at</c> + TTL configurável por tenant, com default de 48h
/// quando o tenant não possui configuração própria (DD-004, ADR-0006, VAL-ACT-02 — 2026-06-15).
/// Ciclo de vida: emitido → expirado (purge pós-expiração) | emitido → usado (fora deste worker).
/// </summary>
/// <remarks>
/// O consumo (<c>used_at</c>) é responsabilidade do activity-management (Req 7.4).
/// Sem dependências de EF Core ou infraestrutura.
/// O TTL é recebido como parâmetro; o domínio não decide o valor (VAL-ACT-02).
/// </remarks>
public sealed class DigestActionToken
{

    /// <summary>Identificador único do token (UUID).</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador do tenant.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Identificador do usuário destinatário.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Identificador lógico da atividade vinculada (sem FK física — DD-001).</summary>
    public Guid ActivityId { get; private set; }

    /// <summary>Tipo de ação: <see cref="ActionType.Complete"/> ou <see cref="ActionType.Reschedule"/>.</summary>
    public ActionType Action { get; private set; }

    /// <summary>Hash SHA-256 do token opaco. Único campo relacionado ao token persistido (DD-007).</summary>
    public byte[] TokenHash { get; private set; } = [];

    /// <summary>Data/hora de expiração do token (<c>created_at</c> + TTL configurável — DD-004, VAL-ACT-02).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Data/hora de uso do token. Setado pelo activity-management (fora deste worker — Req 7.4).
    /// <c>null</c> enquanto não utilizado.
    /// </summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Data/hora de criação do token.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Construtor privado sem parâmetros para hidratação pelo EF Core.
    /// Não deve ser usado diretamente; use <see cref="Issue"/> para criar novas instâncias.
    /// </summary>
#pragma warning disable CS8618 // EF Core preenche as propriedades após a construção
    private DigestActionToken() { }
#pragma warning restore CS8618

    private DigestActionToken(
        Guid id,
        Guid tenantId,
        Guid userId,
        Guid activityId,
        ActionType action,
        byte[] tokenHash,
        DateTimeOffset createdAt,
        TimeSpan ttl)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        ActivityId = activityId;
        Action = action;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = createdAt.Add(ttl);
    }

    /// <summary>
    /// Emite um novo <see cref="DigestActionToken"/> a partir de um <see cref="ActionToken"/> VO.
    /// Extrai o hash do VO; o token em claro nunca é armazenado na entidade.
    /// O TTL é configurável por tenant e resolvido pela camada Application (VAL-ACT-02).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant. Não pode ser vazio.</param>
    /// <param name="userId">Identificador do usuário. Não pode ser vazio.</param>
    /// <param name="activityId">Identificador da atividade. Não pode ser vazio.</param>
    /// <param name="action">Tipo de ação (<see cref="ActionType.Complete"/> ou <see cref="ActionType.Reschedule"/>).</param>
    /// <param name="token">Token emitido pelo <c>IActionTokenFactory</c>. O hash é extraído aqui.</param>
    /// <param name="ttl">
    /// Tempo de vida do token. Deve ser positivo.
    /// Resolvido pela camada Application: setting do tenant quando presente, ou default de 48h (VAL-ACT-02).
    /// </param>
    /// <exception cref="ArgumentException">Quando algum GUID é vazio, <paramref name="token"/> é nulo ou <paramref name="ttl"/> é não-positivo.</exception>
    public static DigestActionToken Issue(
        Guid tenantId,
        Guid userId,
        Guid activityId,
        ActionType action,
        ActionToken token,
        TimeSpan ttl)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));
        if (userId == Guid.Empty)
            throw new ArgumentException("user_id não pode ser vazio.", nameof(userId));
        if (activityId == Guid.Empty)
            throw new ArgumentException("activity_id não pode ser vazio.", nameof(activityId));
        ArgumentNullException.ThrowIfNull(token);
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "O TTL do token deve ser positivo.");

        return new DigestActionToken(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            userId: userId,
            activityId: activityId,
            action: action,
            tokenHash: token.TokenHash,
            createdAt: DateTimeOffset.UtcNow,
            ttl: ttl);
    }

    /// <summary>
    /// Retorna <c>true</c> se o token já expirou no instante fornecido.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) => now > ExpiresAt;
}
