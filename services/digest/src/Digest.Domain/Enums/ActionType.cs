namespace Digest.Domain.Enums;

/// <summary>
/// Tipo de ação possível em um token de um clique do digest.
/// Mapeado na coluna <c>action</c> de <c>digest_action_tokens</c>
/// (design §7: <c>CHECK (action IN ('complete','reschedule'))</c>).
/// </summary>
public enum ActionType
{
    /// <summary>Conclusão da atividade vinculada ao token.</summary>
    Complete,

    /// <summary>Reagendamento da atividade vinculada ao token.</summary>
    Reschedule,
}
