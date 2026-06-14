namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// INV-11: quando há ≥ 1 contato, exatamente 1 deve ser principal.
/// Mapeia: Req 16.2, OP-ERR-009.
/// </summary>
public sealed class PrimaryContactRequiredException()
    : DomainException(
        "Exatamente um contato principal é obrigatório quando há contatos vinculados (INV-11, OP-ERR-009).");
