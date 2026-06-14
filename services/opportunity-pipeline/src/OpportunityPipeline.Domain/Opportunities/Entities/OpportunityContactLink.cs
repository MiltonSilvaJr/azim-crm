namespace OpportunityPipeline.Domain.Opportunities.Entities;

/// <summary>
/// Vínculo a um contato da conta (mantido por account-management).
/// Não armazena PII — apenas o contact_id.
/// INV-11: exatamente 1 principal quando há ≥ 1 contato (garantido no aggregate).
/// Mapeia: Req 16, INV-11, design §4.2.
/// </summary>
public sealed class OpportunityContactLink
{
    /// <summary>Identificador único do vínculo.</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant ao qual pertence.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Oportunidade à qual pertence.</summary>
    public Guid OpportunityId { get; init; }

    /// <summary>Referência ao contato (sem PII — apenas ID).</summary>
    public Guid ContactId { get; init; }

    /// <summary>Se este é o contato principal.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Data/hora de criação do vínculo.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Inicializa o vínculo de contato.</summary>
    public OpportunityContactLink(
        Guid id,
        Guid tenantId,
        Guid opportunityId,
        Guid contactId,
        bool isPrimary)
    {
        Id = id;
        TenantId = tenantId;
        OpportunityId = opportunityId;
        ContactId = contactId;
        IsPrimary = isPrimary;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Promove este contato como principal.</summary>
    internal void MakePrimary() => IsPrimary = true;

    /// <summary>Remove o status de principal.</summary>
    internal void RemovePrimary() => IsPrimary = false;
}
