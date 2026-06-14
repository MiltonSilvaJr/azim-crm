namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// INV-2: stage é sempre obrigatório e não nulo.
/// Mapeia: Req 5, OP-ERR-001.
/// </summary>
public sealed class StageRequiredException()
    : DomainException("stage é obrigatório (INV-2, OP-ERR-001).");
