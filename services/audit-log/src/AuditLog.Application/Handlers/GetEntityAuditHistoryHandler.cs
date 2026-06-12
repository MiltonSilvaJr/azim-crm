using AuditLog.Application.Abstractions;
using AuditLog.Application.Queries;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.ValueObjects;
using MediatR;

namespace AuditLog.Application.Handlers;

/// <summary>
/// Handler da <see cref="GetEntityAuditHistoryQuery"/>.
/// Retorna o histórico de auditoria de uma entidade específica (REQ-007.3).
/// <para>
/// Não altera estado da trilha — operação idempotente (REQ-007.5).
/// </para>
/// </summary>
public sealed class GetEntityAuditHistoryHandler
    : IRequestHandler<GetEntityAuditHistoryQuery, PagedResult<AuditLogAggregate>>
{
    private readonly IAuditLogRepository _repository;
    private readonly ITenantContext _tenantContext;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public GetEntityAuditHistoryHandler(
        IAuditLogRepository repository,
        ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);

        _repository = repository;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Usa <see cref="IAuditLogRepository.FindByEntityAsync"/> para histórico completo da entidade
    /// e depois pagina em memória; para volumes grandes a paginação no repositório é preferível
    /// (refatoração possível em Wave futura sem alterar o contrato desta query).
    /// </remarks>
    public async Task<PagedResult<AuditLogAggregate>> Handle(
        GetEntityAuditHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantIdValue = _tenantContext.TenantId
            ?? throw new InvalidOperationException(
                "TenantId ausente no contexto ao processar GetEntityAuditHistoryQuery.");

        var tenantId = TenantId.From(tenantIdValue);
        var entityRef = EntityReference.Create(request.EntityType, request.EntityId);

        var allItems = await _repository.FindByEntityAsync(tenantId, entityRef, cancellationToken);

        // Aplica filtros opcionais de período
        IEnumerable<AuditLogAggregate> filtered = allItems;

        if (request.From.HasValue)
            filtered = filtered.Where(a => a.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            filtered = filtered.Where(a => a.CreatedAt <= request.To.Value);

        // Mantém ordenação created_at desc (garantida pelo repositório, reaplica após filtros)
        var sorted = filtered.OrderByDescending(a => a.CreatedAt).ToList();

        var skip = (request.Page - 1) * request.PageSize;
        var page = sorted.Skip(skip).Take(request.PageSize).ToList();

        return new PagedResult<AuditLogAggregate>(page, request.Page, request.PageSize, sorted.Count);
    }
}
