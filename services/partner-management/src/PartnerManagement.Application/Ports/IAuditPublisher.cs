namespace PartnerManagement.Application.Ports;

/// <summary>
/// Porta de publicação de auditoria append-only.
/// Toda escrita em parceiros gera um registro de auditoria via esta interface (RNF 2).
/// Implementada na camada Infrastructure (<c>AuditPublisher</c>), usando <c>PartnerPiiMasker</c>
/// para garantir que <c>delta_json</c> não contenha PII em claro (DD-008, RNF 4).
/// Mapeia: RNF 2, RNF 3, design §6.6.
/// </summary>
public interface IAuditPublisher
{
    /// <summary>
    /// Publica um registro de auditoria para a ação realizada sobre uma entidade.
    /// </summary>
    /// <param name="entityType">Tipo da entidade auditada (ex.: "Partner").</param>
    /// <param name="entityId">Identificador da entidade.</param>
    /// <param name="tenantId">Tenant da operação.</param>
    /// <param name="action">Ação realizada (ex.: "Create", "Update", "Deactivate").</param>
    /// <param name="deltaJson">JSON com as alterações (PII mascarada antes de chamar).</param>
    /// <param name="actorId">Usuário que realizou a ação.</param>
    /// <param name="correlationId">Identificador de correlação da requisição.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task PublishAsync(
        string entityType,
        Guid entityId,
        Guid tenantId,
        string action,
        string deltaJson,
        Guid actorId,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
