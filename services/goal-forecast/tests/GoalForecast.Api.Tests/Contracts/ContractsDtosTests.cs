using System.Reflection;
using GoalForecast.Contracts;

namespace GoalForecast.Api.Tests.Contracts;

/// <summary>
/// Testes de TASK-21: verifica que os DTOs de Contracts usam long para valores
/// monetários, que campos anuláveis estão corretos, e que GoalUpdatedEvent tem
/// todos os campos do design §9.1.
///
/// Mapeia: TASK-21, design §8, §9.1, RNF 4.
/// </summary>
public sealed class ContractsDtosTests
{
    // ── GoalDto ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GoalDto.ValorMeta deve ser long (centavos inteiros, RNF 4)")]
    public void GoalDto_ValorMeta_ShouldBeLong()
    {
        var prop = typeof(GoalDto).GetProperty(nameof(GoalDto.ValorMeta));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            because: "valorMeta deve ser centavos inteiros — nunca decimal nem double (RNF 4)");
    }

    [Fact(DisplayName = "GoalDto não deve expor TenantId ou Id no construtor de request (over-posting)")]
    public void GoalDto_ShouldNotAcceptTenantIdInCreateRequest()
    {
        // CreateOrUpdateGoalRequest não deve ter TenantId — proteção de over-posting (design §10)
        var requestType = typeof(CreateOrUpdateGoalRequest);
        var tenantProp = requestType.GetProperty("TenantId");
        tenantProp.Should().BeNull(
            because: "TenantId nunca deve ser aceito no payload — sempre do token JWT (Req 12.1)");
    }

    // ── ForecastPanelResponse ────────────────────────────────────────────────

    [Fact(DisplayName = "ForecastPanelResponse.ValorMeta deve ser long? (anulável, DD-006)")]
    public void ForecastPanelResponse_ValorMeta_ShouldBeNullableLong()
    {
        var prop = typeof(ForecastPanelResponse).GetProperty(nameof(ForecastPanelResponse.ValorMeta));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long?),
            because: "valorMeta é nulo quando não há meta cadastrada (DD-006, Req 6)");
    }

    [Fact(DisplayName = "ForecastPanelResponse.Gap deve ser long? (anulável, DD-006)")]
    public void ForecastPanelResponse_Gap_ShouldBeNullableLong()
    {
        var prop = typeof(ForecastPanelResponse).GetProperty(nameof(ForecastPanelResponse.Gap));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long?),
            because: "gap é nulo quando não há meta (DD-006)");
    }

    [Fact(DisplayName = "ForecastPanelResponse.PctAtingimento deve ser double? (anulável)")]
    public void ForecastPanelResponse_PctAtingimento_ShouldBeNullableDouble()
    {
        var prop = typeof(ForecastPanelResponse).GetProperty(nameof(ForecastPanelResponse.PctAtingimento));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(double?),
            because: "pctAtingimento é nulo sem meta ou com meta zero");
    }

    [Fact(DisplayName = "ForecastPanelResponse.PipelineUnavailable deve ser bool")]
    public void ForecastPanelResponse_PipelineUnavailable_ShouldBeBool()
    {
        var prop = typeof(ForecastPanelResponse).GetProperty(nameof(ForecastPanelResponse.PipelineUnavailable));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(bool),
            because: "pipelineUnavailable sinaliza degradação (DD-007)");
    }

    // ── ForecastAggregateResponse ────────────────────────────────────────────

    [Fact(DisplayName = "ForecastAggregateResponse.ValorMetaAgregado deve ser long")]
    public void ForecastAggregateResponse_ValorMetaAgregado_ShouldBeLong()
    {
        var prop = typeof(ForecastAggregateResponse).GetProperty(nameof(ForecastAggregateResponse.ValorMetaAgregado));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            because: "valorMetaAgregado em centavos inteiros (RN-027, PBT-02)");
    }

    // ── DigestBlockResponse ──────────────────────────────────────────────────

    [Fact(DisplayName = "DigestBlockResponse.Present deve ser bool")]
    public void DigestBlockResponse_Present_ShouldBeBool()
    {
        var prop = typeof(DigestBlockResponse).GetProperty(nameof(DigestBlockResponse.Present));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(bool),
            because: "present é o sinal de ausência do bloco (Req 9.2, RN-018)");
    }

    [Fact(DisplayName = "DigestBlockResponse campos monetários devem ser long?")]
    public void DigestBlockResponse_MonetaryFields_ShouldBeNullableLong()
    {
        var type = typeof(DigestBlockResponse);
        var monetaryFields = new[] { "ValorMeta", "Realizado", "Gap", "PipelineDisponivel" };
        foreach (var fieldName in monetaryFields)
        {
            var prop = type.GetProperty(fieldName);
            prop.Should().NotBeNull(because: $"{fieldName} deve existir em DigestBlockResponse");
            prop!.PropertyType.Should().Be(typeof(long?),
                because: $"{fieldName} é nulo quando Present=false (Req 9.2)");
        }
    }

    // ── GoalUpdatedEvent ─────────────────────────────────────────────────────

    [Fact(DisplayName = "GoalUpdatedEvent deve ter todos os campos canônicos do design §9.1")]
    public void GoalUpdatedEvent_ShouldHaveAllCanonicalFields()
    {
        var type = typeof(GoalUpdatedEvent);

        // Campos canônicos conforme design §9.1
        type.GetProperty(nameof(GoalUpdatedEvent.EventId))!.PropertyType.Should().Be(typeof(Guid));
        type.GetProperty(nameof(GoalUpdatedEvent.TenantId))!.PropertyType.Should().Be(typeof(Guid));
        type.GetProperty(nameof(GoalUpdatedEvent.GoalId))!.PropertyType.Should().Be(typeof(Guid));
        type.GetProperty(nameof(GoalUpdatedEvent.BuId))!.PropertyType.Should().Be(typeof(Guid));
        type.GetProperty(nameof(GoalUpdatedEvent.OwnerId))!.PropertyType.Should().Be(typeof(Guid?));
        type.GetProperty(nameof(GoalUpdatedEvent.Year))!.PropertyType.Should().Be(typeof(int));
        type.GetProperty(nameof(GoalUpdatedEvent.Month))!.PropertyType.Should().Be(typeof(int));
        type.GetProperty(nameof(GoalUpdatedEvent.Action))!.PropertyType.Should().Be(typeof(string));
        type.GetProperty(nameof(GoalUpdatedEvent.ValorMetaAnterior))!.PropertyType.Should().Be(typeof(long?));
        type.GetProperty(nameof(GoalUpdatedEvent.ValorMetaNovo))!.PropertyType.Should().Be(typeof(long));
        type.GetProperty(nameof(GoalUpdatedEvent.OccurredAt))!.PropertyType.Should().Be(typeof(DateTimeOffset));
    }

    [Fact(DisplayName = "GoalUpdatedEvent.ValorMetaNovo deve ser long (nunca double)")]
    public void GoalUpdatedEvent_ValorMetaNovo_ShouldBeLong()
    {
        var prop = typeof(GoalUpdatedEvent).GetProperty(nameof(GoalUpdatedEvent.ValorMetaNovo));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            because: "valorMetaNovo deve ser long — risco RISK-GOAL-05 (serialização como double)");
    }

    // ── Contracts não tem dependências internas ──────────────────────────────

    [Fact(DisplayName = "Assembly Contracts não deve referenciar Domain, Application, Infrastructure ou Api")]
    public void Contracts_Assembly_ShouldHaveNoDependencyOnInternalProjects()
    {
        var contractsAssembly = typeof(GoalDto).Assembly;
        var referencedNames = contractsAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        var forbidden = new[] { "GoalForecast.Domain", "GoalForecast.Application", "GoalForecast.Infrastructure", "GoalForecast.Api" };
        foreach (var name in forbidden)
        {
            referencedNames.Should().NotContain(name,
                because: $"Contracts não pode depender de {name} (DD-001, design §3)");
        }
    }
}
