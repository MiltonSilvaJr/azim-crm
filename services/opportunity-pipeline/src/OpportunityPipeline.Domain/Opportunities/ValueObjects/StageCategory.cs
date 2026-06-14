namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Categoria de ciclo de vida da oportunidade.
/// Governa a máquina de estados (Req 6, design §4.5).
/// Mapeia: INV-2, design §4.3.
/// </summary>
public enum StageCategory
{
    /// <summary>Oportunidade em andamento (negociação ativa).</summary>
    Open,

    /// <summary>Oportunidade encerrada como ganha (estado terminal).</summary>
    Won,

    /// <summary>Oportunidade encerrada como perdida (estado terminal).</summary>
    Lost
}
