namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// INV-6: expected_close_date obrigatória quando stage ≥ "Proposta Enviada".
/// Mapeia: Req 9, OP-ERR-005.
/// </summary>
public sealed class ExpectedCloseDateRequiredException()
    : DomainException(
        "expected_close_date é obrigatória neste estágio (stage ≥ Proposta Enviada) (INV-6, OP-ERR-005).");
