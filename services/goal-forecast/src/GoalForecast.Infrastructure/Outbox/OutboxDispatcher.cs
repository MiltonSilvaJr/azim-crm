using System.Text.Json;
using System.Text.Json.Serialization;
using GoalForecast.Application.Ports;
using GoalForecast.Domain.Common;
using GoalForecast.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoalForecast.Infrastructure.Outbox;

/// <summary>
/// Implementação concreta da porta <see cref="IOutboxDispatcher"/>.
/// Serializa domain events e os grava em <c>outbox_events</c> na mesma
/// transação do <see cref="GoalForecastDbContext"/> corrente (RNF 5, design §6.6).
///
/// Atomicidade garantida: o INSERT em <c>outbox_events</c> ocorre antes do
/// <c>SaveChangesAsync</c> do repositório. Rollback reverte ambas as escritas.
///
/// Serialização: <c>long</c> nativo via System.Text.Json — sem conversão para <c>double</c>.
/// <c>JsonSerializerOptions</c> configurado com <c>NumberHandling = Strict</c> para
/// detectar qualquer vazamento numérico em tempo de serialização (PBT-05).
///
/// Mapeia: Req 10, RNF 5, design §6.6, TASK-20.
/// </summary>
public sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly GoalForecastDbContext _dbContext;

    /// <summary>
    /// Opções JSON com tratamento numérico estrito — <c>long</c> serializado como inteiro JSON.
    /// Sem <c>WriteIndented</c> para compactar payload.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.Strict,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Cria o dispatcher com o DbContext corrente (injetado por escopo de request).
    /// </summary>
    public OutboxDispatcher(GoalForecastDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions);
        var eventType = domainEvent.GetType().Name;
        var tenantId = ExtractTenantId(domainEvent);

        // INSERT direto via SQL parametrizado na transação corrente do DbContext.
        // Usa FormattableString para EF Core tratar como parâmetro seguro (sem SQL injection).
        await _dbContext.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO outbox_events (event_id, tenant_id, event_type, payload)
             VALUES ({domainEvent.EventId}, {tenantId}, {eventType}, {payload}::jsonb)
             """,
            cancellationToken);
    }

    /// <summary>
    /// Extrai <c>tenant_id</c> do domain event via reflexão.
    /// Domain events de goal-forecast sempre carregam TenantId (design §4.4).
    /// </summary>
    private static Guid ExtractTenantId(IDomainEvent domainEvent)
    {
        var prop = domainEvent.GetType().GetProperty("TenantId");
        if (prop?.GetValue(domainEvent) is Guid tenantId)
            return tenantId;

        return Guid.Empty;
    }
}
