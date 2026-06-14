namespace ActivityManagement.Application.Activities.Queries;

using ActivityManagement.Contracts.Activities;
using ActivityManagement.Domain.Activities;

/// <summary>
/// Utilitário de mapeamento do agregado <see cref="Activity"/> para o DTO <see cref="ActivityResponse"/>.
/// Centraliza a conversão usada por handlers de query e pela camada API.
/// Mapeia: design §8, TASK-18.
/// </summary>
public static class ActivityMapper
{
    /// <summary>
    /// Mapeia o agregado <see cref="Activity"/> para <see cref="ActivityResponse"/>.
    /// </summary>
    /// <param name="activity">Agregado de atividade.</param>
    /// <returns>DTO de resposta.</returns>
    public static ActivityResponse ToResponse(Activity activity) => new(
        Id:            activity.Id,
        Type:          activity.Type.Value,
        Title:         activity.Title,
        Description:   activity.Description,
        DueAt:         activity.DueAt.Value,
        Status:        activity.Status.Value,
        Priority:      activity.Priority.Value,
        OwnerId:       activity.OwnerId,
        BuId:          activity.BuId,
        TenantId:      activity.TenantId,
        OpportunityId: activity.OpportunityLink?.OpportunityId,
        AccountId:     activity.AccountLink?.AccountId,
        CompletedAt:   activity.CompletedAt,
        CreatedAt:     activity.CreatedAt,
        UpdatedAt:     activity.UpdatedAt);
}
