namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// INV-3: origin_channel é sempre obrigatório.
/// Mapeia: Req 4, OP-ERR-003.
/// </summary>
public sealed class OriginChannelRequiredException()
    : DomainException("origin_channel é obrigatório (INV-3, OP-ERR-003).");
