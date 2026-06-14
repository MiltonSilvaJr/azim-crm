namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// Tentativa de mutação em snapshot imutável de comissão.
/// Mapeia: INV-12, RNF 5, DD-002, OP-ERR-017 (indireto).
/// </summary>
public sealed class SnapshotImmutableException()
    : DomainException(
        "Snapshot de comissão é imutável — alterações não são permitidas após o congelamento (RNF 5, DD-002).");
