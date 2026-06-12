using AuditLog.Application.Abstractions;
using AuditLog.Application.Queries;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuditLog.Application.Handlers;

/// <summary>
/// Handler da <see cref="ListAuditLogsQuery"/>.
/// Consulta paginada da trilha de auditoria com filtros opcionais (REQ-007).
/// Aplica filtro adicional de BU para papel <c>GestorBU</c> (DD-008, RISK-AUDIT-05).
/// <para>
/// Não altera estado da trilha — operação idempotente (REQ-007.5).
/// </para>
/// </summary>
public sealed class ListAuditLogsHandler : IRequestHandler<ListAuditLogsQuery, PagedResult<AuditLogAggregate>>
{
    private readonly IAuditLogRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IUserContext _userContext;
    private readonly IBuScopeResolver _buScopeResolver;
    private readonly ILogger<ListAuditLogsHandler> _logger;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public ListAuditLogsHandler(
        IAuditLogRepository repository,
        ITenantContext tenantContext,
        IUserContext userContext,
        IBuScopeResolver buScopeResolver,
        ILogger<ListAuditLogsHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(buScopeResolver);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _buScopeResolver = buScopeResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AuditLogAggregate>> Handle(
        ListAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantIdValue = _tenantContext.TenantId
            ?? throw new InvalidOperationException(
                "TenantId ausente no contexto ao processar ListAuditLogsQuery.");

        var tenantId = TenantId.From(tenantIdValue);

        // DD-008: filtro adicional de BU para papel GestorBU
        var isGestorBu = string.Equals(
            _userContext.Role,
            AuditRoles.GestorBU,
            StringComparison.OrdinalIgnoreCase);

        if (isGestorBu)
        {
            return await HandleGestorBuAsync(request, tenantId, cancellationToken);
        }

        var (items, totalCount) = await _repository.ListAsync(
            tenantId,
            entityType: request.EntityType,
            entityId: request.EntityId,
            actorId: request.UserId,
            from: request.From,
            to: request.To,
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return new PagedResult<AuditLogAggregate>(items, request.Page, request.PageSize, totalCount);
    }

    /// <summary>
    /// Trata consulta para papel GestorBU com filtro de escopo de BU (DD-008).
    /// <para>
    /// RISK-AUDIT-05: implementação MVP itera por entity_id do escopo de BU.
    /// Otimização de query (denormalizar bu_id ou query IN) pendente para Wave futura.
    /// </para>
    /// </summary>
    private async Task<PagedResult<AuditLogAggregate>> HandleGestorBuAsync(
        ListAuditLogsQuery request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var allowedEntityIds = await _buScopeResolver.ResolveAsync(cancellationToken);

        // Sem restrição de BU (IBuScopeResolver retornou null): comportamento idêntico ao TenantAdmin
        if (allowedEntityIds is null)
        {
            var (items, totalCount) = await _repository.ListAsync(
                tenantId,
                entityType: request.EntityType,
                entityId: request.EntityId,
                actorId: request.UserId,
                from: request.From,
                to: request.To,
                page: request.Page,
                pageSize: request.PageSize,
                cancellationToken: cancellationToken);

            return new PagedResult<AuditLogAggregate>(items, request.Page, request.PageSize, totalCount);
        }

        // Com restrição: aplica filtro de entityId do scope de BU
        // Se a query já filtra por entityId específico, verifica se está no escopo de BU
        if (request.EntityId.HasValue)
        {
            if (!allowedEntityIds.Contains(request.EntityId.Value))
            {
                _logger.LogInformation(
                    "GestorBU requisitou entity_id fora do seu escopo de BU. EntityId={EntityId}",
                    request.EntityId.Value);

                return new PagedResult<AuditLogAggregate>(
                    Array.Empty<AuditLogAggregate>(), request.Page, request.PageSize, 0);
            }

            var (items, totalCount) = await _repository.ListAsync(
                tenantId,
                entityType: request.EntityType,
                entityId: request.EntityId,
                actorId: request.UserId,
                from: request.From,
                to: request.To,
                page: request.Page,
                pageSize: request.PageSize,
                cancellationToken: cancellationToken);

            return new PagedResult<AuditLogAggregate>(items, request.Page, request.PageSize, totalCount);
        }

        // Sem entityId específico: itera pelo escopo (MVP — RISK-AUDIT-05)
        // Coleta todos os registros do scope e pagina em memória.
        // Otimização futura: query SQL com IN ou denormalização de bu_id.
        var allItems = new List<AuditLogAggregate>();
        foreach (var entityId in allowedEntityIds)
        {
            var (scopeItems, _) = await _repository.ListAsync(
                tenantId,
                entityType: request.EntityType,
                entityId: entityId,
                actorId: request.UserId,
                from: request.From,
                to: request.To,
                page: 1,
                pageSize: ListAuditLogsQueryValidator.MaxPageSize,
                cancellationToken: cancellationToken);

            allItems.AddRange(scopeItems);
        }

        // Ordena por created_at desc e pagina
        var sorted = allItems
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        var skip = (request.Page - 1) * request.PageSize;
        var page = sorted.Skip(skip).Take(request.PageSize).ToList();

        return new PagedResult<AuditLogAggregate>(page, request.Page, request.PageSize, sorted.Count);
    }
}
