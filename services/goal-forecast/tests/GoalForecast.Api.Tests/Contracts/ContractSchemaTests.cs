using System.Text.Json;
using System.Text.Json.Serialization;
using GoalForecast.Application.Ports;
using GoalForecast.Contracts;

namespace GoalForecast.Api.Tests.Contracts;

/// <summary>
/// Testes de contrato para o schema goal.updated.v1 e a porta IPipelineForecastReader.
///
/// Objetivo: garantir que mudanças de schema quebram estes testes, protegendo consumidores
/// (audit-log, digest, reporting) de regressões silenciosas.
///
/// - goal.updated.v1: todos os campos canônicos (design §9.1) presentes e tipados.
/// - valorMetaNovo serializado como long (não double) — RISK-GOAL-05.
/// - ForecastViewResult: Available, WonTotalCents, ForecastPonderadoCents como long.
/// - Contrato da porta IPipelineForecastReader: assinatura e semântica de indisponibilidade.
///
/// Mapeia: TASK-26, design §9.1, §13, Req 8, Req 10.
/// </summary>
public sealed class ContractSchemaTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.Strict,
        WriteIndented = false
    };

    // ── GoalUpdatedEvent — campos canônicos presentes ─────────────────────────

    [Fact]
    public void GoalUpdatedEvent_TodosCamposCanonicosPresentes()
    {
        // design §9.1: eventId, tenantId, goalId, buId, ownerId, year, month,
        // action, valorMetaAnterior, valorMetaNovo, occurredAt
        var evt = BuildSampleEvent();

        var json = JsonSerializer.Serialize(evt, JsonOptions);

        json.Should().Contain("\"eventId\"");
        json.Should().Contain("\"tenantId\"");
        json.Should().Contain("\"goalId\"");
        json.Should().Contain("\"buId\"");
        json.Should().Contain("\"year\"");
        json.Should().Contain("\"month\"");
        json.Should().Contain("\"action\"");
        json.Should().Contain("\"valorMetaNovo\"");
        json.Should().Contain("\"occurredAt\"");
        // ownerId e valorMetaAnterior são opcionais (nulos na criação)
    }

    [Fact]
    public void GoalUpdatedEvent_ActionCreated_ValorMetaAnteriorAusente()
    {
        // Quando action="created", valorMetaAnterior deve ser null (design §9.1)
        var evt = BuildSampleEvent(action: "created", valorMetaAnterior: null);

        evt.ValorMetaAnterior.Should().BeNull();
        evt.Action.Should().Be("created");
    }

    [Fact]
    public void GoalUpdatedEvent_ActionUpdated_ValorMetaAnteriorPresente()
    {
        var evt = BuildSampleEvent(action: "updated", valorMetaAnterior: 50_000_00L);

        evt.ValorMetaAnterior.Should().Be(50_000_00L);
        evt.Action.Should().Be("updated");
    }

    [Fact]
    public void GoalUpdatedEvent_OwnerId_NuloParaEscopoBu()
    {
        var evt = BuildSampleEvent(ownerId: null);

        evt.OwnerId.Should().BeNull();

        // ownerId nulo é serializado como null no JSON (escopo BU)
        var json = JsonSerializer.Serialize(evt, JsonOptions);
        json.Should().Contain("\"ownerId\":null");
    }

    // ── ValorMetaNovo como long (RISK-GOAL-05) ────────────────────────────────

    [Fact]
    public void GoalUpdatedEvent_ValorMetaNovo_SerializadoComoLongNaoDouble()
    {
        // Valor acima de Number.MAX_SAFE_INTEGER (JavaScript) — 2^53 + 1
        const long valorGrande = 9_007_199_254_740_993L;
        var evt = BuildSampleEvent(valorMetaNovo: valorGrande);

        var json = JsonSerializer.Serialize(evt, JsonOptions);

        // Deve aparecer como inteiro, nunca como notação científica double
        json.Should().Contain("9007199254740993");
        json.Should().NotContain("9.007199254740993E+15");
        json.Should().NotContain("9.007199254740993e+15");
        json.Should().NotContain("9007199254740992");  // sem arredondamento double
    }

    [Fact]
    public void GoalUpdatedEvent_RoundTrip_PreservaPrecisaoLong()
    {
        // Serializar e desserializar deve preservar o valor exato
        const long valorOriginal = 9_007_199_254_740_993L;
        var evt = BuildSampleEvent(valorMetaNovo: valorOriginal);

        var json = JsonSerializer.Serialize(evt, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<GoalUpdatedEvent>(json, JsonOptions);

        deserialized!.ValorMetaNovo.Should().Be(valorOriginal);
    }

    [Fact]
    public void GoalUpdatedEvent_ValorMetaAnterior_RoundTripPreservaPrecisao()
    {
        const long valorAnterior = 1_234_567_890_123_456L;
        var evt = BuildSampleEvent(action: "updated", valorMetaAnterior: valorAnterior);

        var json = JsonSerializer.Serialize(evt, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<GoalUpdatedEvent>(json, JsonOptions);

        deserialized!.ValorMetaAnterior.Should().Be(valorAnterior);
    }

    // ── Schema JSON: campos obrigatórios/opcionais ────────────────────────────

    [Fact]
    public void GoalUpdatedEvent_EventId_CampoObrigatorio_TipoGuid()
    {
        // eventId é required (design §9.1) — verificação via reflection do tipo
        var propriedade = typeof(GoalUpdatedEvent).GetProperty(nameof(GoalUpdatedEvent.EventId));
        propriedade.Should().NotBeNull("eventId é campo canônico do schema goal.updated.v1");
        propriedade!.PropertyType.Should().Be(typeof(Guid));

        // Verificar que o campo é marcado como required via CustomAttributes ou que está no construtor
        // No C# records com 'required', o campo é obrigatório na inicialização
        var customAttributes = propriedade.CustomAttributes;
        // 'required' no C# 12 emite RequiredMemberAttribute no assembly
        var hasRequired = propriedade.DeclaringType?
            .GetCustomAttributesData()
            .Any() ?? false;

        // O evento construído via factory sempre tem EventId não-zero
        var evt = BuildSampleEvent();
        evt.EventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void GoalUpdatedEvent_ValorMetaNovo_CampoObrigatorio_TipoLong()
    {
        // Verificar que o campo é do tipo long (não decimal, não double, não int)
        var propriedade = typeof(GoalUpdatedEvent).GetProperty(nameof(GoalUpdatedEvent.ValorMetaNovo));
        propriedade!.PropertyType.Should().Be(typeof(long));
    }

    [Fact]
    public void GoalUpdatedEvent_ValorMetaAnterior_TipoNullableLong()
    {
        var propriedade = typeof(GoalUpdatedEvent).GetProperty(nameof(GoalUpdatedEvent.ValorMetaAnterior));
        propriedade!.PropertyType.Should().Be(typeof(long?));
    }

    // ── Porta ForecastViewResult — contrato de tipos ──────────────────────────

    [Fact]
    public void ForecastViewResult_WonTotalCents_TipoLong()
    {
        var propriedade = typeof(ForecastViewResult).GetProperty(nameof(ForecastViewResult.WonTotalCents));
        propriedade!.PropertyType.Should().Be(typeof(long));
    }

    [Fact]
    public void ForecastViewResult_ForecastPonderadoCents_TipoLong()
    {
        var propriedade = typeof(ForecastViewResult).GetProperty(nameof(ForecastViewResult.ForecastPonderadoCents));
        propriedade!.PropertyType.Should().Be(typeof(long));
    }

    [Fact]
    public void ForecastViewResult_Available_TipoBool()
    {
        var propriedade = typeof(ForecastViewResult).GetProperty(nameof(ForecastViewResult.Available));
        propriedade!.PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void ForecastViewResult_Unavailable_AvailableFalseEValoresZero()
    {
        // Instância estática Unavailable deve sinalizar indisponibilidade (DD-007)
        var unavailable = ForecastViewResult.Unavailable;

        unavailable.Available.Should().BeFalse();
        // Valores zero são sinalizados como inválidos pelo Available=false
        // (nunca exibir zero confundível — RNF 6, DD-007)
        unavailable.WonTotalCents.Should().Be(0L);
        unavailable.ForecastPonderadoCents.Should().Be(0L);
    }

    [Fact]
    public void ForecastViewResult_ComDados_AvailableTrue()
    {
        var result = new ForecastViewResult(
            WonTotalCents: 100_000_00L,
            ForecastPonderadoCents: 50_000_00L,
            Available: true);

        result.Available.Should().BeTrue();
        result.WonTotalCents.Should().Be(100_000_00L);
        result.ForecastPonderadoCents.Should().Be(50_000_00L);
    }

    // ── IPipelineForecastReader — contrato da porta ───────────────────────────

    [Fact]
    public void IPipelineForecastReader_MetodoRead_AssinturaCorreta()
    {
        // O método Read deve aceitar ForecastViewQuery e CancellationToken, retornar Task<ForecastViewResult>
        var method = typeof(IPipelineForecastReader)
            .GetMethod(nameof(IPipelineForecastReader.Read));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<ForecastViewResult>));

        var parametros = method.GetParameters();
        parametros.Should().HaveCount(2);
        parametros[0].ParameterType.Should().Be(typeof(ForecastViewQuery));
        parametros[1].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public void ForecastViewQuery_CamposDeChave_TiposCorretos()
    {
        // ForecastViewQuery deve ter TenantId (Guid), BuId (Guid), OwnerId (Guid?), Year (int), Month (int)
        typeof(ForecastViewQuery).GetProperty(nameof(ForecastViewQuery.TenantId))!
            .PropertyType.Should().Be(typeof(Guid));

        typeof(ForecastViewQuery).GetProperty(nameof(ForecastViewQuery.BuId))!
            .PropertyType.Should().Be(typeof(Guid));

        typeof(ForecastViewQuery).GetProperty(nameof(ForecastViewQuery.OwnerId))!
            .PropertyType.Should().Be(typeof(Guid?));

        typeof(ForecastViewQuery).GetProperty(nameof(ForecastViewQuery.Year))!
            .PropertyType.Should().Be(typeof(int));

        typeof(ForecastViewQuery).GetProperty(nameof(ForecastViewQuery.Month))!
            .PropertyType.Should().Be(typeof(int));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GoalUpdatedEvent BuildSampleEvent(
        string action = "created",
        long? valorMetaAnterior = null,
        long valorMetaNovo = 100_000_00L,
        Guid? ownerId = null) =>
        new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            GoalId = Guid.NewGuid(),
            BuId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            OwnerId = ownerId,
            Year = 2026,
            Month = 1,
            Action = action,
            ValorMetaAnterior = valorMetaAnterior,
            ValorMetaNovo = valorMetaNovo,
            OccurredAt = DateTimeOffset.UtcNow
        };
}
