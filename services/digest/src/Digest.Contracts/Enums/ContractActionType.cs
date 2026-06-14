namespace Digest.Contracts.Enums;

/// <summary>
/// Tipo de ação do link autenticado de um clique no digest (requirements §7, design §4.2).
/// Publicado em contratos externos e usado pelo módulo activity-management para validação do token.
/// Espelha <c>Digest.Domain.Enums.ActionType</c> sem depender do projeto de domínio (Contracts → ∅).
/// </summary>
public enum ContractActionType
{
    /// <summary>
    /// Ação de conclusão da atividade (marcar como concluída).
    /// Corresponde a <c>"complete"</c> no contrato do digest (Req 7.2).
    /// </summary>
    Complete,

    /// <summary>
    /// Ação de reagendamento da atividade.
    /// Corresponde a <c>"reschedule"</c> no contrato do digest (Req 7.3).
    /// </summary>
    Reschedule,
}
