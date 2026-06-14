namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de entrada para reagendamento de atividade (Req 8).
/// Mapeia: design §8, ACT-ERR-003/011, TASK-18.
/// </summary>
/// <param name="DueAt">Nova data de vencimento.</param>
public sealed record RescheduleRequest(DateTimeOffset DueAt);
