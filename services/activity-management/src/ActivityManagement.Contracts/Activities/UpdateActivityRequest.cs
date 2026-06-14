namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de entrada para atualização de atividade (Req 2).
/// Mapeia: design §8, ACT-ERR-001/002/005/006/011, TASK-18.
/// </summary>
/// <param name="Type">Tipo da atividade.</param>
/// <param name="Title">Título não vazio (I1).</param>
/// <param name="DueAt">Instante de vencimento.</param>
/// <param name="Priority">Prioridade (low|medium|high).</param>
/// <param name="Description">Descrição opcional (PII potencial).</param>
/// <param name="OpportunityId">Oportunidade vinculada (opcional).</param>
/// <param name="AccountId">Conta vinculada (opcional).</param>
public sealed record UpdateActivityRequest(
    string          Type,
    string          Title,
    DateTimeOffset  DueAt,
    string?         Priority      = null,
    string?         Description   = null,
    Guid?           OpportunityId = null,
    Guid?           AccountId     = null);
