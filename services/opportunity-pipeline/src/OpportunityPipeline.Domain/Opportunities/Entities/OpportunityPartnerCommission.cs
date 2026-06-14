using OpportunityPipeline.Domain.Opportunities.Exceptions;
using OpportunityPipeline.Domain.Opportunities.ValueObjects;

namespace OpportunityPipeline.Domain.Opportunities.Entities;

/// <summary>
/// Vínculo de comissão de parceiro. Dois estados:
/// - Projetada (is_snapshot = false): editável, recalculada a cada mudança.
/// - Snapshot (is_snapshot = true): imutável após Freeze(); representa comissão congelada ao ganhar.
/// Mapeia: Req 11, Req 14, RNF 5, INV-12, DD-002, DD-003, design §4.2.
/// </summary>
public sealed class OpportunityPartnerCommission
{
    /// <summary>Identificador único.</summary>
    public Guid Id { get; }

    /// <summary>Tenant ao qual pertence.</summary>
    public Guid TenantId { get; }

    /// <summary>Oportunidade à qual pertence.</summary>
    public Guid OpportunityId { get; }

    /// <summary>Parceiro.</summary>
    public Guid PartnerId { get; }

    /// <summary>Termos de comissão vigentes.</summary>
    public CommissionTerms Terms { get; private set; }

    /// <summary>Resultado do cálculo de comissão.</summary>
    public CommissionCalculation Calculation { get; private set; }

    /// <summary>True quando congelado como snapshot imutável.</summary>
    public bool IsSnapshot { get; private set; }

    /// <summary>Data/hora do congelamento (null enquanto projetada).</summary>
    public DateTimeOffset? SnapshotAt { get; private set; }

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Inicializa como entidade projetada (is_snapshot = false).</summary>
    public OpportunityPartnerCommission(
        Guid id,
        Guid tenantId,
        Guid opportunityId,
        Guid partnerId,
        CommissionTerms terms,
        CommissionCalculation calculation)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(calculation);

        Id = id;
        TenantId = tenantId;
        OpportunityId = opportunityId;
        PartnerId = partnerId;
        Terms = terms;
        Calculation = calculation;
        IsSnapshot = false;
        SnapshotAt = null;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // Construtor privado para reconstituição de snapshot
    private OpportunityPartnerCommission(
        Guid id,
        Guid tenantId,
        Guid opportunityId,
        Guid partnerId,
        CommissionTerms terms,
        CommissionCalculation calculation,
        bool isSnapshot,
        DateTimeOffset? snapshotAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        OpportunityId = opportunityId;
        PartnerId = partnerId;
        Terms = terms;
        Calculation = calculation;
        IsSnapshot = isSnapshot;
        SnapshotAt = snapshotAt;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Congela a comissão projetada criando um novo registro snapshot imutável.
    /// Copiando os termos e cálculo vigentes para o snapshot.
    /// </summary>
    /// <param name="snapshotAt">Data/hora do congelamento.</param>
    /// <returns>Novo registro com is_snapshot = true.</returns>
    /// <exception cref="SnapshotImmutableException">Se já for snapshot.</exception>
    public OpportunityPartnerCommission Freeze(DateTimeOffset snapshotAt)
    {
        if (IsSnapshot)
            throw new SnapshotImmutableException();

        return new OpportunityPartnerCommission(
            id: Guid.NewGuid(),
            tenantId: TenantId,
            opportunityId: OpportunityId,
            partnerId: PartnerId,
            terms: Terms,
            calculation: Calculation,
            isSnapshot: true,
            snapshotAt: snapshotAt,
            createdAt: snapshotAt);
    }

    /// <summary>
    /// Atualiza termos e cálculo da comissão projetada.
    /// Snapshot é imutável e lança exceção.
    /// </summary>
    /// <exception cref="SnapshotImmutableException">Se já for snapshot.</exception>
    public OpportunityPartnerCommission WithUpdatedCalculation(
        CommissionTerms newTerms,
        CommissionCalculation newCalculation)
    {
        if (IsSnapshot)
            throw new SnapshotImmutableException();

        Terms = newTerms;
        Calculation = newCalculation;
        return this;
    }
}
