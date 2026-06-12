namespace AuditLog.Contracts;

/// <summary>
/// Dados necessários para registrar uma entrada de auditoria.
/// Imutável por design: todos os campos são init-only (positional record).
/// <para>
/// Campos derivados do contexto autenticado — como <c>TenantId</c> e <c>CorrelationId</c> —
/// são intencionalmente omitidos. O consumidor de <see cref="IAuditWriter"/> não os informa;
/// a implementação os resolve a partir do contexto de execução.
/// </para>
/// </summary>
/// <param name="EntityType">
/// Nome do tipo da entidade afetada (ex.: <c>"Order"</c>, <c>"Customer"</c>).
/// </param>
/// <param name="EntityId">
/// Identificador da entidade afetada. Representado como string para suportar
/// diferentes tipos de chave (Guid, int, string composta, etc.).
/// </param>
/// <param name="Action">Operação que gerou o registro.</param>
/// <param name="UserId">
/// Identificador do usuário que executou a operação, derivado do token de autenticação.
/// </param>
/// <param name="RawBefore">
/// Estado da entidade antes da operação. <see langword="null"/> para criações.
/// Os valores são mantidos como <see cref="object"/> para suportar qualquer tipo primitivo
/// serializável. A implementação aplica mascaramento de PII antes de persistir.
/// </param>
/// <param name="RawAfter">
/// Estado da entidade após a operação. <see langword="null"/> para exclusões.
/// </param>
public sealed record AuditEntryRequest(
    string EntityType,
    string EntityId,
    AuditAction Action,
    string UserId,
    IReadOnlyDictionary<string, object?>? RawBefore,
    IReadOnlyDictionary<string, object?>? RawAfter);
