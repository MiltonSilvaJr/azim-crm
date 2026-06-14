using GoalForecast.Domain.ValueObjects;

namespace GoalForecast.Application.Common;

/// <summary>
/// DTO de saída do aggregate Goal para handlers e queries.
/// Todos os valores monetários em centavos inteiros (<see cref="long"/>).
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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
