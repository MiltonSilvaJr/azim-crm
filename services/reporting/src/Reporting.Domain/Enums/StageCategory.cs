namespace Reporting.Domain.Enums;

/// <summary>
/// Categoria de estágio de uma oportunidade.
///
/// Refletida da origem (BC-01) sem reclassificação.
/// O reporting nunca redefine a categoria — apenas a lê (P2, design §4.3).
///
/// Mapeia: TASK-03, design §4.3, DD-003.
/// </summary>
public enum StageCategory
{
    /// <summary>Oportunidade em aberto (em negociação).</summary>
    Open,

    /// <summary>Oportunidade ganha (fechada com sucesso).</summary>
    Won,

    /// <summary>Oportunidade perdida.</summary>
    Lost
}
