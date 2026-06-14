namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de resposta representando uma atividade comercial.
/// Não contém <c>title</c> nem <c>description</c> em logs — apenas em responses HTTP.
/// Mapeia: design §8, TASK-18.
/// </summary>
/// <param name="Id">Identificador único da atividade.</param>
/// <param name="Type">Tipo da atividade (meeting|follow_up|call|email|task).</param>
/// <param name="Title">Título da atividade (PII potencial — não logar).</param>
/// <param name="Description">Descrição opcional (PII potencial — não logar).</param>
/// <param name="DueAt">Instante de vencimento.</param>
/// <param name="Status">Status corrente na máquina de estados.</param>
/// <param name="Priority">Prioridade (low|medium|high).</param>
/// <param name="OwnerId">Usuário responsável.</param>
/// <param name="BuId">Business Unit.</param>
/// <param name="TenantId">Tenant ao qual a atividade pertence.</param>
/// <param name="OpportunityId">Oportunidade vinculada (opcional).</param>
/// <param name="AccountId">Conta vinculada (opcional).</param>
/// <param name="CompletedAt">Instante de conclusão (apenas quando status=completed).</param>
/// <param name="CreatedAt">Instante de criação.</param>
/// <param name="UpdatedAt">Instante da última modificação.</param>
public sealed record ActivityResponse(
    Guid            Id,
    string          Type,
    string          Title,
    string?         Description,
    DateTimeOffset  DueAt,
    string          Status,
    string          Priority,
    Guid            OwnerId,
    Guid            BuId,
    Guid            TenantId,
    Guid?           OpportunityId,
    Guid?           AccountId,
    DateTimeOffset? CompletedAt,
    DateTimeOffset  CreatedAt,
    DateTimeOffset  UpdatedAt);
