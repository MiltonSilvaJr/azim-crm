namespace Authentication.Application.Services;

/// <summary>
/// Porta interna de emissão de eventos auditáveis de autenticação.
///
/// Eventos auditáveis (não são eventos de domínio) são entregues ao módulo
/// <c>audit-log</c> de forma append-only (RNF 10, design.md § 9.1).
///
/// Cada evento registra: <c>event_type</c>, <c>tenant_id</c>, <c>user_id</c>,
/// <c>occurred_at</c>, <c>correlation_id</c>. Nunca contém <c>identity_uid</c>,
/// senha, token completo ou PII além do necessário (RNF 10.2).
///
/// Mapeia: RNF 10, design.md § 4.4, § 9.1.
/// </summary>
public interface IAuditEventEmitter
{
    /// <summary>
    /// Emite um evento auditável de forma assíncrona.
    /// </summary>
    /// <param name="eventType">
    /// Tipo do evento (ex.: "session_revoked", "invite_activated",
    /// "password_reset_requested", "password_reset_completed").
    /// </param>
    /// <param name="tenantId">UUID do tenant no qual o evento ocorreu.</param>
    /// <param name="userId">UUID do usuário interno (nunca identity_uid).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task EmitAsync(
        string eventType,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
