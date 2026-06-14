namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// INV-7: encerrar como perdida exige loss_reason não nulo.
/// Mapeia: Req 10, OP-ERR-006.
/// </summary>
public sealed class LossReasonRequiredException()
    : DomainException("loss_reason_id é obrigatório ao encerrar oportunidade como perdida (INV-7, OP-ERR-006).");
