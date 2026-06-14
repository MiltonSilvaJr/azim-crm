using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using PartnerManagement.Domain.Partners;
using PartnerManagement.Domain.Partners.ValueObjects;
using Xunit;

namespace PartnerManagement.Domain.Tests.Partners.PropertyTests;

/// <summary>
/// PBT-02: idempotência de inativação/reativação do Aggregate Root <see cref="Partner"/>.
/// Propriedades:
/// - Aplicar <see cref="Partner.Deactivate"/> N≥1 vezes sempre resulta em <see cref="PartnerStatus.Inactive"/>.
/// - Aplicar <see cref="Partner.Reactivate"/> N≥1 vezes sempre resulta em <see cref="PartnerStatus.Active"/>.
/// - Transições não lançam exceção em nenhum caso.
/// Mapeia: PBT-02, Req 3.5, design §4.5, DD-006, TASK-08.
/// </summary>
[Trait("Category", "PBT")]
public sealed class PartnerIdempotencyPbt
{
    private static readonly Guid _tenantId = Guid.NewGuid();
    private static readonly Guid _actorId = Guid.NewGuid();

    private sealed class AlwaysValidRoleProvider : ICanonicalRoleProvider
    {
        public bool IsCanonical(string role, Guid tenantId) => true;
    }

    private static Partner BuildActivePartner()
    {
        return Partner.Create(
            tenantId: _tenantId,
            name: "PBT Partner",
            role: "Indicador",
            commissionDefaults: CommissionDefaults.Default,
            contact: null,
            notes: null,
            roleProvider: new AlwaysValidRoleProvider(),
            createdBy: _actorId);
    }

    /// <summary>
    /// PBT-02a: N≥1 aplicações de Deactivate sempre resultam em Inactive.
    /// O gerador produz N no intervalo [1; 20].
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-02a: N Deactivates → sempre Inactive")]
    public bool NDeactivates_AlwaysResultsInInactive(PositiveInt n)
    {
        Partner partner = BuildActivePartner();
        int count = Math.Min(n.Get, 20);

        for (int i = 0; i < count; i++)
        {
            partner.Deactivate();
        }

        return partner.Status == PartnerStatus.Inactive;
    }

    /// <summary>
    /// PBT-02b: N≥1 aplicações de Reactivate (após uma inativação) sempre resultam em Active.
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-02b: N Reactivates (após Deactivate) → sempre Active")]
    public bool NReactivates_AfterDeactivate_AlwaysResultsInActive(PositiveInt n)
    {
        Partner partner = BuildActivePartner();
        partner.Deactivate();

        int count = Math.Min(n.Get, 20);
        for (int i = 0; i < count; i++)
        {
            partner.Reactivate();
        }

        return partner.Status == PartnerStatus.Active;
    }

    /// <summary>
    /// PBT-02c: N alternâncias Deactivate/Reactivate nunca lançam exceção.
    /// Retorna true porque bool methods são propriedades de identidade no FsCheck.
    /// </summary>
    [Property(MaxTest = 200, DisplayName = "PBT-02c: alternâncias Deactivate/Reactivate nunca lançam exceção")]
    public bool AlternatingTransitions_NeverThrow(PositiveInt n)
    {
        Partner partner = BuildActivePartner();
        int count = Math.Min(n.Get, 10);

        for (int i = 0; i < count; i++)
        {
            if (i % 2 == 0)
            {
                partner.Deactivate();
            }
            else
            {
                partner.Reactivate();
            }
        }

        // Se chegou aqui sem exceção, a propriedade é satisfeita
        return true;
    }
}
