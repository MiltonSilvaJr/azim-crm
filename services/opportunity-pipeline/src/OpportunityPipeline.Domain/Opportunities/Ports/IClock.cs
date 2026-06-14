namespace OpportunityPipeline.Domain.Opportunities.Ports;

/// <summary>
/// Abstração de relógio para testabilidade do domínio.
/// Permite injeção de tempo determinístico em testes.
/// Mapeia: design §5.4 (StagnationSpecification, OverdueSpecification).
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante atual em UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Retorna a data atual em UTC.</summary>
    DateOnly Today { get; }
}
