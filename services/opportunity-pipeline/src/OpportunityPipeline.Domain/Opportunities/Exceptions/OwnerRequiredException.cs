namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// INV-1: owner_id é obrigatório e não pode ser nulo.
/// Mapeia: Req 2, OP-ERR-002.
/// </summary>
public sealed class OwnerRequiredException()
    : DomainException("owner_id é obrigatório e deve referenciar usuário ativo na BU (OP-ERR-002, INV-1).");
