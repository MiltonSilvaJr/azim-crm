namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de entrada para criação de atividade (Req 1).
/// Mapeia: design §8, ACT-ERR-001/002/005/006/010, TASK-18.
/// </summary>
/// <param name="Type">Tipo da atividade: meeting|follow_up|call|email|task.</param>
/// <param name="Title">Título não vazio (I1).</param>
/// <param name="DueAt">Instante de vencimento obrigatório (ACT-ERR-010).</param>
/// <param name="OwnerId">Usuário responsável pela atividade.</param>
/// <param name="Priority">Prioridade; usa medium quando omitida (I3).</param>
/// <param name="Description">Descrição opcional (PII potencial).</param>
/// <param name="OpportunityId">Oportunidade vinculada (opcional — mesmo tenant).</param>
/// <param name="AccountId">Conta vinculada (opcional — mesmo tenant).</param>
public sealed record CreateActivityRequest(
    string          Type,
    string          Title,
    DateTimeOffset  DueAt,
    Guid            OwnerId,
    string?         Priority      = null,
    string?         Description   = null,
    Guid?           OpportunityId = null,
    Guid?           AccountId     = null);
