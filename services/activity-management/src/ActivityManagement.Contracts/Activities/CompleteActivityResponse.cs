namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de resposta para conclusão de atividade (Req 6, Req 6.5).
/// Inclui sugestão de próxima atividade quando aplicável.
/// Mapeia: design §8, TASK-18.
/// </summary>
/// <param name="ActivityId">ID da atividade concluída.</param>
/// <param name="CompletedAt">Instante de conclusão efetiva.</param>
/// <param name="WasAlreadyCompleted">Verdadeiro quando atividade já estava concluída (idempotência).</param>
/// <param name="Suggestion">Sugestão de próxima atividade (opcional — Req 9).</param>
public sealed record CompleteActivityResponse(
    Guid            ActivityId,
    DateTimeOffset  CompletedAt,
    bool            WasAlreadyCompleted,
    NextActivitySuggestion? Suggestion = null);

/// <summary>
/// Pré-preenchimento de vínculo sugerido ao concluir atividade vinculada a oportunidade aberta (Req 9).
/// Não cria atividade — UI-driven (DD-006).
/// </summary>
/// <param name="OpportunityId">Oportunidade sugerida.</param>
/// <param name="AccountId">Conta associada (opcional).</param>
public sealed record NextActivitySuggestion(Guid OpportunityId, Guid? AccountId = null);
