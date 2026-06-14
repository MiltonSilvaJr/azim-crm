namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// INV-4: canal Parceiro exige partner_id não nulo.
/// Mapeia: Req 4.2, OP-ERR-004.
/// </summary>
public sealed class PartnerRequiredException()
    : DomainException("partner_id é obrigatório quando o canal de origem é Parceiro (INV-4, OP-ERR-004).");
