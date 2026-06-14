using FluentAssertions;
using Organization.Application.Mapping;
using Organization.Contracts.Events;
using Organization.Domain.Events;
using Xunit;

namespace Organization.Api.Tests.Events;

/// <summary>
/// Testes de contrato para os 5 eventos de integração <c>*.v1</c> (TASK-24, §9, DD-005).
/// Valida: envelope obrigatório, tipo correto, ausência de PII na carga.
/// ST-01 — schema e envelope.
/// ST-03 — mapeamento domain event → integration event.
/// </summary>
public sealed class IntegrationEventEnvelopeSchemaTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _correlationId = Guid.NewGuid();
    private readonly Guid _messageId = Guid.NewGuid();
    private readonly Guid _buId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _invitationId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    // ── bu.created.v1 ─────────────────────────────────────────────────────────

    [Fact]
    public void BuCreated_Envelope_ContainsAllRequiredFields()
    {
        // Arrange
        var domainEvent = new BusinessUnitCreated(_tenantId, _buId, "BU Vendas", _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);

        // Assert — campos obrigatórios do envelope (design §9, DD-005)
        envelope.MessageId.Should().Be(_messageId);
        envelope.TenantId.Should().Be(_tenantId);
        envelope.CorrelationId.Should().Be(_correlationId);
        envelope.OccurredAt.Should().Be(_now);
        envelope.EventType.Should().Be("bu.created.v1");
    }

    [Fact]
    public void BuCreated_Payload_ContainsExpectedFields()
    {
        // Arrange
        var domainEvent = new BusinessUnitCreated(_tenantId, _buId, "BU Vendas", _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);

        // Assert — carga contém tenant_id, bu_id, name
        envelope.Payload.Should().ContainKey("tenant_id").WhoseValue.Should().Be(_tenantId);
        envelope.Payload.Should().ContainKey("bu_id").WhoseValue.Should().Be(_buId);
        envelope.Payload.Should().ContainKey("name").WhoseValue.Should().Be("BU Vendas");
    }

    [Fact]
    public void BuCreated_Payload_ContainsNoPii()
    {
        // Arrange
        var domainEvent = new BusinessUnitCreated(_tenantId, _buId, "BU Vendas", _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(envelope.Payload);

        // Assert — sem e-mail ou display_name (RNF 3)
        payloadJson.Should().NotContain("email", because: "PII proibida na carga do evento");
        payloadJson.Should().NotContain("display_name", because: "PII proibida na carga do evento");
        payloadJson.Should().NotContain("password", because: "credencial proibida na carga");
    }

    // ── user.invited.v1 ───────────────────────────────────────────────────────

    [Fact]
    public void UserInvited_Envelope_ContainsAllRequiredFields()
    {
        // Arrange
        var domainEvent = new UserInvited(_tenantId, _invitationId, _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);

        // Assert
        envelope.MessageId.Should().Be(_messageId);
        envelope.TenantId.Should().Be(_tenantId);
        envelope.CorrelationId.Should().Be(_correlationId);
        envelope.OccurredAt.Should().Be(_now);
        envelope.EventType.Should().Be("user.invited.v1");
    }

    [Fact]
    public void UserInvited_Payload_ContainsOnlyIdentifiers_NoPii()
    {
        // Arrange — convite com e-mail (que NÃO deve aparecer no evento de integração)
        var domainEvent = new UserInvited(_tenantId, _invitationId, _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(envelope.Payload);

        // Assert — carga contém apenas identificadores
        envelope.Payload.Should().ContainKey("tenant_id").WhoseValue.Should().Be(_tenantId);
        envelope.Payload.Should().ContainKey("invitation_id").WhoseValue.Should().Be(_invitationId);

        // Sem PII (RNF 3, DD-005)
        payloadJson.Should().NotContain("email", because: "e-mail é PII e não deve aparecer no evento");
        payloadJson.Should().NotContain("display_name", because: "PII proibida na carga");
        envelope.Payload.Should().NotContainKey("email");
        envelope.Payload.Should().NotContainKey("display_name");
    }

    // ── user.activated.v1 ────────────────────────────────────────────────────

    [Fact]
    public void UserActivated_Envelope_ContainsAllRequiredFields()
    {
        // Arrange
        var memberships = new List<(Guid BuId, string Role)> { (_buId, "Vendedor") };
        var domainEvent = new UserActivated(_tenantId, _userId, memberships, _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);

        // Assert
        envelope.MessageId.Should().Be(_messageId);
        envelope.TenantId.Should().Be(_tenantId);
        envelope.CorrelationId.Should().Be(_correlationId);
        envelope.OccurredAt.Should().Be(_now);
        envelope.EventType.Should().Be("user.activated.v1");
    }

    [Fact]
    public void UserActivated_Payload_ContainsMemberships_NoPii()
    {
        // Arrange
        var memberships = new List<(Guid BuId, string Role)>
        {
            (_buId, "Vendedor"),
            (Guid.NewGuid(), "GestorBU"),
        };
        var domainEvent = new UserActivated(_tenantId, _userId, memberships, _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(envelope.Payload);

        // Assert — campos esperados
        envelope.Payload.Should().ContainKey("tenant_id").WhoseValue.Should().Be(_tenantId);
        envelope.Payload.Should().ContainKey("user_id").WhoseValue.Should().Be(_userId);
        envelope.Payload.Should().ContainKey("memberships");

        // Sem PII (RNF 3)
        payloadJson.Should().NotContain("email", because: "e-mail é PII e não deve aparecer");
        payloadJson.Should().NotContain("display_name", because: "PII proibida na carga");
        envelope.Payload.Should().NotContainKey("email");
        envelope.Payload.Should().NotContainKey("display_name");
    }

    // ── user.deactivated.v1 ──────────────────────────────────────────────────

    [Fact]
    public void UserDeactivated_Envelope_ContainsAllRequiredFields()
    {
        // Arrange
        var domainEvent = new UserDeactivated(_tenantId, _userId, _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);

        // Assert
        envelope.MessageId.Should().Be(_messageId);
        envelope.TenantId.Should().Be(_tenantId);
        envelope.CorrelationId.Should().Be(_correlationId);
        envelope.OccurredAt.Should().Be(_now);
        envelope.EventType.Should().Be("user.deactivated.v1");
    }

    [Fact]
    public void UserDeactivated_Payload_ContainsOnlyIdentifiers_NoPii()
    {
        // Arrange
        var domainEvent = new UserDeactivated(_tenantId, _userId, _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(envelope.Payload);

        // Assert
        envelope.Payload.Should().ContainKey("tenant_id").WhoseValue.Should().Be(_tenantId);
        envelope.Payload.Should().ContainKey("user_id").WhoseValue.Should().Be(_userId);

        payloadJson.Should().NotContain("email");
        payloadJson.Should().NotContain("display_name");
        envelope.Payload.Should().NotContainKey("email");
        envelope.Payload.Should().NotContainKey("display_name");
    }

    // ── user.role_changed.v1 ─────────────────────────────────────────────────
    // ST-03 — mapeamento MembershipRoleChanged → user.role_changed.v1

    [Fact]
    public void MembershipRoleChanged_MapsTo_UserRoleChangedV1()
    {
        // Arrange (ST-03 Red → Green)
        var domainEvent = new MembershipRoleChanged(_tenantId, _userId, _buId, "GestorBU", _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);

        // Assert — tipo correto
        envelope.EventType.Should().Be("user.role_changed.v1",
            because: "design §9 define o mapeamento MembershipRoleChanged → user.role_changed.v1");
    }

    [Fact]
    public void UserRoleChanged_Envelope_ContainsAllRequiredFields()
    {
        // Arrange
        var domainEvent = new MembershipRoleChanged(_tenantId, _userId, _buId, "GestorBU", _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);

        // Assert — todos os campos obrigatórios do envelope
        envelope.MessageId.Should().Be(_messageId);
        envelope.TenantId.Should().Be(_tenantId);
        envelope.CorrelationId.Should().Be(_correlationId);
        envelope.OccurredAt.Should().Be(_now);
    }

    [Fact]
    public void UserRoleChanged_Payload_ContainsAllExpectedFields_NoPii()
    {
        // Arrange
        var domainEvent = new MembershipRoleChanged(_tenantId, _userId, _buId, "GestorBU", _now);

        // Act
        var envelope = IntegrationEventMapper.ToEnvelope(domainEvent, _messageId, _tenantId, _correlationId);
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(envelope.Payload);

        // Assert — campos de payload conforme design §9
        envelope.Payload.Should().ContainKey("tenant_id").WhoseValue.Should().Be(_tenantId);
        envelope.Payload.Should().ContainKey("user_id").WhoseValue.Should().Be(_userId);
        envelope.Payload.Should().ContainKey("bu_id").WhoseValue.Should().Be(_buId);
        envelope.Payload.Should().ContainKey("role").WhoseValue.Should().Be("GestorBU");

        // Sem PII (RNF 3)
        payloadJson.Should().NotContain("email");
        payloadJson.Should().NotContain("display_name");
        envelope.Payload.Should().NotContainKey("email");
        envelope.Payload.Should().NotContainKey("display_name");
    }

    // ── Casos negativos ──────────────────────────────────────────────────────

    [Fact]
    public void UnknownDomainEvent_ThrowsArgumentException()
    {
        // Arrange — evento desconhecido (sem mapeamento registrado)
        var unknownEvent = new UnknownTestEvent(_tenantId, _now);

        // Act
        var act = () => IntegrationEventMapper.ToEnvelope(unknownEvent, _messageId, _tenantId, _correlationId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UnknownTestEvent*");
    }

    // ── Helper: evento de teste desconhecido ─────────────────────────────────

    private sealed record UnknownTestEvent(Guid TenantId, DateTimeOffset OccurredAt)
        : Organization.Domain.Events.IDomainEvent;
}
