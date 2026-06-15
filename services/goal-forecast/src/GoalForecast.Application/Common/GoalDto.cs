using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Application.Common;

/// <summary>
/// DTO de saída do aggregate Goal para handlers e queries.
/// Todos os valores monetários em centavos inteiros (<see cref="long"/>).
/// Currency ISO-4217 explícita (ADR-0008).
/// Mapeia: design §8.1, §8.3, TASK-09.
/// </summary>
public sealed record GoalDto(
    Guid Id,
    Guid TenantId,
    string Scope,
    Guid BuId,
    Guid? OwnerId,
    int Year,
    int Month,
    long ValorMeta,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
