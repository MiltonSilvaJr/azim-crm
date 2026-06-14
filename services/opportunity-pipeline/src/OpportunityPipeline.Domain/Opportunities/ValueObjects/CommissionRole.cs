namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Papel do parceiro na comissão (Req 11.1, design §4.3).
/// </summary>
public enum CommissionRole
{
    /// <summary>Parceiro indicador.</summary>
    Indicador,

    /// <summary>Parceiro revendedor.</summary>
    Revendedor,

    /// <summary>Parceiro distribuidor.</summary>
    Distribuidor,

    /// <summary>Parceiro integrador.</summary>
    Integrador
}
