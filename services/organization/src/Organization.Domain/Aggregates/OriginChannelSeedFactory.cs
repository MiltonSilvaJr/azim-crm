namespace Organization.Domain.Aggregates;

/// <summary>
/// Seed inicial de canais de origem por BU.
/// Canais editáveis pelo TAdmin após criação.
/// </summary>
public static class OriginChannelSeedFactory
{
    /// <summary>
    /// Retorna a lista de canais de origem padrão para BUs criadas via seed.
    /// </summary>
    public static IReadOnlyList<string> CreateDefaultChannels() =>
    [
        "Indicação",
        "Site",
        "Redes Sociais",
        "Prospecção Ativa",
    ];
}
