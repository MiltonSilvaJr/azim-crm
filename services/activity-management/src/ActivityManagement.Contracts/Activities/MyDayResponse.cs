namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de resposta da visão "Meu dia" (Req 5).
/// Atividades agrupadas em três faixas calculadas no fuso IANA do tenant (DD-008).
/// Mapeia: design §8, TASK-18.
/// </summary>
/// <param name="Overdue">Atividades vencidas (dueAt anterior ao início do dia local do tenant).</param>
/// <param name="Today">Atividades do dia corrente do tenant.</param>
/// <param name="Upcoming">Atividades com dueAt após o dia corrente do tenant.</param>
public sealed record MyDayResponse(
    IReadOnlyList<ActivityResponse> Overdue,
    IReadOnlyList<ActivityResponse> Today,
    IReadOnlyList<ActivityResponse> Upcoming);
