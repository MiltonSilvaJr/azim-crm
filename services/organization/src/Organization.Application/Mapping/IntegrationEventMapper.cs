using Organization.Contracts.Events;
using Organization.Domain.Events;

namespace Organization.Application.Mapping;

/// <summary>
/// Mapeia domain events para envelopes de eventos de integração <c>*.v1</c> (DD-005, §9).
/// O mapper é puro (sem dependências de infraestrutura) e testável de forma unitária.
/// </summary>
/// <remarks>
/// Convenção de nomes: domain events em PascalCase → integração em dot.case.v1.
/// Mapeamentos oficiais (design §9):
/// <list type="bullet">
///   <item><c>BusinessUnitCreated</c> → <c>bu.created.v1</c></item>
///   <item><c>UserInvited</c>         → <c>user.invited.v1</c></item>
///   <item><c>UserActivated</c>       → <c>user.activated.v1</c></item>
///   <item><c>UserDeactivated</c>     → <c>user.deactivated.v1</c></item>
///   <item><c>MembershipRoleChanged</c> → <c>user.role_changed.v1</c></item>
/// </list>
/// </remarks>
public static class IntegrationEventMapper
{
    /// <summary>
    /// Converte um domain event em um envelope de evento de integração.
    /// </summary>
    /// <param name="domainEvent">Domain event emitido pelo agregado.</param>
    /// <param name="messageId">Identificador único da mensagem (gerado pelo outbox).</param>
    /// <param name="tenantId">Identificador do tenant dono do evento.</param>
    /// <param name="correlationId">Identificador de correlação para rastreabilidade.</param>
    /// <returns>Envelope com tipo <c>*.v1</c>, carga sem PII e campos obrigatórios preenchidos.</returns>
    /// <exception cref="ArgumentException">Domain event desconhecido — sem mapeamento registrado.</exception>
    public static IntegrationEventEnvelope ToEnvelope(
        IDomainEvent domainEvent,
        Guid messageId,
        Guid tenantId,
        Guid correlationId)
    {
        var (eventType, payload) = domainEvent switch
        {
            BusinessUnitCreated e => (
                "bu.created.v1",
                (IReadOnlyDictionary<string, object?>)BuildBuCreatedPayload(e)),

            UserInvited e => (
                "user.invited.v1",
                (IReadOnlyDictionary<string, object?>)BuildUserInvitedPayload(e)),

            UserActivated e => (
                "user.activated.v1",
                (IReadOnlyDictionary<string, object?>)BuildUserActivatedPayload(e)),

            UserDeactivated e => (
                "user.deactivated.v1",
                (IReadOnlyDictionary<string, object?>)BuildUserDeactivatedPayload(e)),

            MembershipRoleChanged e => (
                "user.role_changed.v1",
                (IReadOnlyDictionary<string, object?>)BuildMembershipRoleChangedPayload(e)),

            _ => throw new ArgumentException(
                $"Domain event sem mapeamento de integração: {domainEvent.GetType().Name}",
                nameof(domainEvent)),
        };

        return new IntegrationEventEnvelope(
            MessageId: messageId,
            EventType: eventType,
            TenantId: tenantId,
            CorrelationId: correlationId,
            OccurredAt: domainEvent.OccurredAt,
            Payload: payload);
    }

    // ── Builders de payload sem PII ───────────────────────────────────────────

    private static Dictionary<string, object?> BuildBuCreatedPayload(BusinessUnitCreated e)
        => new()
        {
            ["tenant_id"] = e.TenantId,
            ["bu_id"] = e.BusinessUnitId,
            ["name"] = e.Name,
        };

    private static Dictionary<string, object?> BuildUserInvitedPayload(UserInvited e)
        => new()
        {
            // Sem e-mail: apenas identificadores (RNF 3, DD-005)
            ["tenant_id"] = e.TenantId,
            ["invitation_id"] = e.InvitationId,
        };

    private static Dictionary<string, object?> BuildUserActivatedPayload(UserActivated e)
    {
        // Memberships sem PII: apenas buId + role (sem e-mail, display_name)
        var memberships = e.Memberships
            .Select(m => (object?)new Dictionary<string, object?>
            {
                ["bu_id"] = m.BuId,
                ["role"] = m.Role,
            })
            .ToList();

        return new()
        {
            ["tenant_id"] = e.TenantId,
            ["user_id"] = e.UserId,
            ["memberships"] = memberships,
        };
    }

    private static Dictionary<string, object?> BuildUserDeactivatedPayload(UserDeactivated e)
        => new()
        {
            ["tenant_id"] = e.TenantId,
            ["user_id"] = e.UserId,
        };

    private static Dictionary<string, object?> BuildMembershipRoleChangedPayload(MembershipRoleChanged e)
        => new()
        {
            ["tenant_id"] = e.TenantId,
            ["user_id"] = e.UserId,
            ["bu_id"] = e.BuId,
            ["role"] = e.Role,
        };
}
