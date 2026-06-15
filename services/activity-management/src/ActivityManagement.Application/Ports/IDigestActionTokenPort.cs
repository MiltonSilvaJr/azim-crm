namespace ActivityManagement.Application.Ports;

/// <summary>
/// Modelo de dados do token de ação do digest retornado pelo port.
/// Não carrega o token em claro — apenas dados validáveis persistidos.
/// <see cref="ActivityId"/> é <c>Guid</c> NOT NULL (referência lógica — DD-001, ADR-0006).
/// Mapeia: design §6.4, DD-003, ADR-0006, Req 7.
/// </summary>
/// <param name="Id">Identificador do registro de token.</param>
/// <param name="TenantId">Tenant ao qual o token pertence.</param>
/// <param name="UserId">Usuário para o qual o token foi emitido.</param>
/// <param name="ActivityId">Atividade referenciada pelo token (NOT NULL — DD-001, ADR-0006).</param>
/// <param name="Action">Ação do token: <c>Complete</c> ou <c>Reschedule</c> (casing do enum digest).</param>
/// <param name="ExpiresAt">Instante de expiração do token.</param>
/// <param name="UsedAt">Instante em que o token foi consumido; nulo quando ainda disponível.</param>
public sealed record DigestActionTokenData(
    Guid             Id,
    Guid             TenantId,
    Guid             UserId,
    Guid             ActivityId,
    string           Action,
    DateTimeOffset   ExpiresAt,
    DateTimeOffset?  UsedAt);

/// <summary>
/// Port de validação e consumo do <c>digest_action_token</c> (DD-003, ADR-0006, Req 7).
/// O token em claro transita apenas no link do digest; esta interface opera sobre o hash SHA-256.
/// Implementado em Infrastructure por <c>DigestActionTokenAdapter</c>.
/// Mapeia: design §6.4, RNF 5, ADR-0006, PBT-02, PBT-03.
/// </summary>
public interface IDigestActionTokenPort
{
    /// <summary>
    /// Localiza o registro de token pelo hash SHA-256 (32 bytes BYTEA) do token opaco apresentado.
    /// Retorna <c>null</c> quando inexistente ou inacessível (anti-enumeração — Req 7.6, PBT-03).
    /// </summary>
    /// <param name="tokenHash">Hash SHA-256 do token opaco (32 bytes).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<DigestActionTokenData?> FindByHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca o token como consumido, preenchendo <c>used_at</c> na transação corrente.
    /// Deve ser chamado apenas quando <see cref="DigestActionTokenData.UsedAt"/> é nulo.
    /// É no-op seguro se <c>used_at</c> já estiver preenchido (uso único garantido por UPDATE WHERE used_at IS NULL).
    /// </summary>
    /// <param name="tokenId">Identificador do registro de token.</param>
    /// <param name="usedAt">Instante do consumo (fornecido via <see cref="IClock"/>).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task MarkUsedAsync(Guid tokenId, DateTimeOffset usedAt, CancellationToken cancellationToken = default);
}
