namespace AuditLog.Contracts;

/// <summary>
/// DTO de resposta de uma entrada da trilha de auditoria (design §8.1, §8.2).
/// Mapeado a partir de <c>AuditLogAggregate</c> na camada Api.
/// <para>
/// Regras de serialização:
/// <list type="bullet">
/// <item><description><see cref="Action"/> serializado em lowercase via <c>JsonStringEnumConverter</c> com <c>JsonNamingPolicy.CamelCase</c>.</description></item>
/// <item><description><see cref="CreatedAt"/> em UTC ISO-8601 (terminando em <c>Z</c>).</description></item>
/// <item><description>Nenhuma PII nos campos (design §10, RNF-002.3).</description></item>
/// </list>
/// </para>
/// </summary>
/// <param name="Id">Identificador único do registro de auditoria.</param>
/// <param name="UserId">Identificador do autor da operação (não é PII — é UUID referencial).</param>
/// <param name="EntityType">Tipo da entidade auditada (ex.: <c>Opportunity</c>).</param>
/// <param name="EntityId">Identificador da entidade auditada.</param>
/// <param name="Action">Tipo de operação: <c>create</c>, <c>update</c> ou <c>delete</c>.</param>
/// <param name="Delta">Diferencial estruturado da entidade (já mascarado de PII).</param>
/// <param name="CreatedAt">Timestamp UTC da persistência (REQ-002.4).</param>
public sealed record AuditLogResponse(
    Guid Id,
    Guid UserId,
    string EntityType,
    Guid EntityId,
    AuditAction Action,
    object? Delta,
    DateTimeOffset CreatedAt);

/// <summary>
/// Envelope paginado de respostas de auditoria (design §8.1).
/// </summary>
/// <param name="Items">Registros da página corrente.</param>
/// <param name="Page">Número da página (base 1).</param>
/// <param name="PageSize">Tamanho da página solicitado.</param>
/// <param name="Total">Total de registros (antes da paginação).</param>
public sealed record PagedAuditLogResponse(
    IReadOnlyList<AuditLogResponse> Items,
    int Page,
    int PageSize,
    int Total);
