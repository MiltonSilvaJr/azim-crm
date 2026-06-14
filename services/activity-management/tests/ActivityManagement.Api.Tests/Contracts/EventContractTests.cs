namespace ActivityManagement.Api.Tests.Contracts;

using System.Reflection;
using System.Text.Json;
using ActivityManagement.Contracts.Events;
using Xunit;

/// <summary>
/// Testes de contrato baseados em schema para os eventos v1 publicados pelo activity-management.
/// Substitui Pact (não disponível no stack atual) por validação explícita de schema.
/// Verifica: envelope canônico, ausência de PII (title/description), versão v1, campos
/// obrigatórios dos consumidores digest e opportunity-pipeline.
///
/// Nota técnica: Pact provider tests serão adicionados em TASK-24 quando o pacote
/// PactNet for incorporado. Esta suite garante contrato estrutural equivalente.
/// Mapeia: TASK-20, design §9, RNF 7.2 (sem PII), Req 14.
/// </summary>
public sealed class EventContractTests
{
    // ── ActivityCreatedV1 ─────────────────────────────────────────────────────

    [Fact]
    public void ActivityCreatedV1_EnvelopeCanonicoCompleto()
    {
        // Arrange
        var evt = BuildActivityCreatedV1();

        // Assert — envelope canônico
        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.Equal("activity.created.v1", evt.EventType);
        Assert.Equal("v1", evt.EventVersion);
        Assert.NotEqual(Guid.Empty, evt.TenantId);
        Assert.NotEqual(Guid.Empty, evt.CorrelationId);
        Assert.NotEqual(default, evt.OccurredAt);
    }

    [Fact]
    public void ActivityCreatedV1_NaoContemTitleNemDescription()
    {
        // Requisito RNF 7.2 e DD-009: payload sem PII
        var type = typeof(ActivityCreatedV1);

        var hasTitle       = type.GetProperty("Title",       BindingFlags.Public | BindingFlags.Instance) is not null;
        var hasDescription = type.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance) is not null;

        Assert.False(hasTitle,       "ActivityCreatedV1 NÃO deve ter propriedade 'Title' (RNF 7.2)");
        Assert.False(hasDescription, "ActivityCreatedV1 NÃO deve ter propriedade 'Description' (RNF 7.2)");
    }

    [Fact]
    public void ActivityCreatedV1_CamposObrigatoriosConsumidorDigest()
    {
        // digest consome: ActivityId, OwnerId, DueAt, TenantId, CorrelationId
        var evt = BuildActivityCreatedV1();

        Assert.NotEqual(Guid.Empty, evt.ActivityId);
        Assert.NotEqual(Guid.Empty, evt.OwnerId);
        Assert.NotEqual(default, evt.DueAt);
        Assert.NotEqual(Guid.Empty, evt.TenantId);
        Assert.NotEqual(Guid.Empty, evt.CorrelationId);
    }

    [Fact]
    public void ActivityCreatedV1_SerializacaoJsonPreservaEnvelope()
    {
        // ASP.NET Core usa camelCase por padrão — usamos as mesmas opções nos testes
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var evt  = BuildActivityCreatedV1();
        var json = JsonSerializer.Serialize(evt, opts);
        var obj  = JsonDocument.Parse(json).RootElement;

        Assert.True(obj.TryGetProperty("eventId",       out _), "'eventId' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("eventType",     out _), "'eventType' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("eventVersion",  out _), "'eventVersion' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("tenantId",      out _), "'tenantId' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("correlationId", out _), "'correlationId' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("occurredAt",    out _), "'occurredAt' obrigatório no envelope");
        Assert.False(obj.TryGetProperty("title",        out _), "Propriedade 'title' NÃO deve existir (RNF 7.2)");
        Assert.False(obj.TryGetProperty("description",  out _), "Propriedade 'description' NÃO deve existir (RNF 7.2)");
    }

    // ── ActivityCompletedV1 ───────────────────────────────────────────────────

    [Fact]
    public void ActivityCompletedV1_EnvelopeCanonicoCompleto()
    {
        var evt = BuildActivityCompletedV1();

        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.Equal("activity.completed.v1", evt.EventType);
        Assert.Equal("v1", evt.EventVersion);
        Assert.NotEqual(Guid.Empty, evt.TenantId);
        Assert.NotEqual(Guid.Empty, evt.CorrelationId);
        Assert.NotEqual(default, evt.OccurredAt);
    }

    [Fact]
    public void ActivityCompletedV1_NaoContemTitleNemDescription()
    {
        var type = typeof(ActivityCompletedV1);

        Assert.False(type.GetProperty("Title",       BindingFlags.Public | BindingFlags.Instance) is not null,
            "ActivityCompletedV1 NÃO deve ter 'Title' (RNF 7.2)");
        Assert.False(type.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance) is not null,
            "ActivityCompletedV1 NÃO deve ter 'Description' (RNF 7.2)");
    }

    [Fact]
    public void ActivityCompletedV1_CamposObrigatoriosConsumidorOpportunityPipeline()
    {
        // opportunity-pipeline consome: ActivityId, OwnerId, CompletedAt, OpportunityId (opcional)
        var evt = BuildActivityCompletedV1();

        Assert.NotEqual(Guid.Empty, evt.ActivityId);
        Assert.NotEqual(Guid.Empty, evt.OwnerId);
        Assert.NotEqual(default, evt.CompletedAt);
    }

    [Fact]
    public void ActivityCompletedV1_SerializacaoJsonPreservaEnvelope()
    {
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var evt  = BuildActivityCompletedV1();
        var json = JsonSerializer.Serialize(evt, opts);
        var obj  = JsonDocument.Parse(json).RootElement;

        Assert.True(obj.TryGetProperty("eventId",      out _), "'eventId' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("eventType",    out _), "'eventType' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("eventVersion", out _), "'eventVersion' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("occurredAt",   out _), "'occurredAt' obrigatório no envelope");
        Assert.True(obj.TryGetProperty("completedAt",  out _), "'completedAt' obrigatório no payload");
        Assert.False(obj.TryGetProperty("title",       out _), "Propriedade 'title' NÃO deve existir (RNF 7.2)");
    }

    // ── ActivityOverdueV1 ─────────────────────────────────────────────────────

    [Fact]
    public void ActivityOverdueV1_EnvelopeCanonicoCompleto()
    {
        var evt = BuildActivityOverdueV1();

        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.Equal("activity.overdue.v1", evt.EventType);
        Assert.Equal("v1", evt.EventVersion);
        Assert.NotEqual(Guid.Empty, evt.TenantId);
        Assert.NotEqual(Guid.Empty, evt.CorrelationId);
        Assert.NotEqual(default, evt.OccurredAt);
    }

    [Fact]
    public void ActivityOverdueV1_NaoContemTitleNemDescription()
    {
        var type = typeof(ActivityOverdueV1);

        Assert.False(type.GetProperty("Title",       BindingFlags.Public | BindingFlags.Instance) is not null,
            "ActivityOverdueV1 NÃO deve ter 'Title' (RNF 7.2)");
        Assert.False(type.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance) is not null,
            "ActivityOverdueV1 NÃO deve ter 'Description' (RNF 7.2)");
    }

    [Fact]
    public void ActivityOverdueV1_ContemScanDateParaDeduplicacao()
    {
        // DD-005: chave de deduplicação (activityId, scanDate)
        var evt = BuildActivityOverdueV1();

        Assert.NotEqual(default, evt.ScanDate);
        Assert.NotEqual(Guid.Empty, evt.ActivityId);
    }

    [Fact]
    public void ActivityOverdueV1_CamposObrigatoriosConsumidorDigest()
    {
        // digest consome: ActivityId, OwnerId, DueAt, ScanDate, TenantId
        var evt = BuildActivityOverdueV1();

        Assert.NotEqual(Guid.Empty, evt.ActivityId);
        Assert.NotEqual(Guid.Empty, evt.OwnerId);
        Assert.NotEqual(default, evt.DueAt);
        Assert.NotEqual(default, evt.ScanDate);
        Assert.NotEqual(Guid.Empty, evt.TenantId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ActivityCreatedV1 BuildActivityCreatedV1() => new()
    {
        EventId       = Guid.NewGuid(),
        TenantId      = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        OccurredAt    = DateTimeOffset.UtcNow,
        ActivityId    = Guid.NewGuid(),
        BuId          = Guid.NewGuid(),
        OwnerId       = Guid.NewGuid(),
        Type          = "meeting",
        DueAt         = DateTimeOffset.UtcNow.AddDays(1),
    };

    private static ActivityCompletedV1 BuildActivityCompletedV1() => new()
    {
        EventId       = Guid.NewGuid(),
        TenantId      = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        OccurredAt    = DateTimeOffset.UtcNow,
        ActivityId    = Guid.NewGuid(),
        OwnerId       = Guid.NewGuid(),
        CompletedAt   = DateTimeOffset.UtcNow,
    };

    private static ActivityOverdueV1 BuildActivityOverdueV1() => new()
    {
        EventId       = Guid.NewGuid(),
        TenantId      = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        OccurredAt    = DateTimeOffset.UtcNow,
        ActivityId    = Guid.NewGuid(),
        OwnerId       = Guid.NewGuid(),
        DueAt         = DateTimeOffset.UtcNow.AddDays(-1),
        ScanDate      = DateOnly.FromDateTime(DateTime.UtcNow),
    };
}
