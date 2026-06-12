using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;

namespace AuditLog.Domain.Repositories;

/// <summary>
/// Contrato do repositório de auditoria.
/// Expõe exclusivamente <see cref="AddAsync"/> e operações de leitura.
/// Operações de mutação (Update/Remove/Delete) são intencionalmente ausentes
/// para reforçar a política append-only no nível do domínio (RNF-001).
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Adiciona um novo registro de auditoria na unidade de trabalho corrente.
    /// Participa da transação da escrita de negócio (DD-001, fail-closed).
    /// </summary>
    /// <param name="auditLog">Registro a ser persistido; não pode ser nulo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task AddAsync(AuditLogAggregate auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o histórico de auditoria de uma entidade específica, ordenado por <c>created_at</c> desc.
    /// </summary>
    /// <param name="tenantId">Tenant do contexto autenticado (filtrado via RLS).</param>
    /// <param name="entityReference">Tipo e identificador da entidade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<IReadOnlyList<AuditLogAggregate>> FindByEntityAsync(
        TenantId tenantId,
        EntityReference entityReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista registros de auditoria com filtros opcionais, paginados e ordenados por <c>created_at</c> desc.
    /// </summary>
    /// <param name="tenantId">Tenant do contexto autenticado (filtrado via RLS).</param>
    /// <param name="entityType">Filtra por tipo de entidade (opcional).</param>
    /// <param name="entityId">Filtra por identificador de entidade (opcional).</param>
    /// <param name="actorId">Filtra por autor (opcional).</param>
    /// <param name="from">Início do intervalo de <c>created_at</c> (opcional).</param>
    /// <param name="to">Fim do intervalo de <c>created_at</c> (opcional).</param>
    /// <param name="page">Número da página, iniciando em 1.</param>
    /// <param name="pageSize">Tamanho da página; máximo 200 (REQ-007.4).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Tupla com a página de itens e a contagem total de registros.</returns>
    Task<(IReadOnlyList<AuditLogAggregate> Items, int TotalCount)> ListAsync(
        TenantId tenantId,
        string? entityType = null,
        Guid? entityId = null,
        Guid? actorId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
