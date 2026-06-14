using System.Reflection;
using FluentAssertions;
using OpportunityPipeline.Contracts.ErrorCodes;
using OpportunityPipeline.Contracts.Events;
using OpportunityPipeline.Contracts.Responses;
using Xunit;

namespace OpportunityPipeline.Api.Tests.Contracts;

/// <summary>
/// Testes de contrato — TASK-19 ST-01.
/// Verifica: money como long (centavos), sem PII nos eventos .v1, catálogo OP-ERR completo.
/// Mapeia: RNF 11.1 (money em centavos), Req 20.3 (sem PII), design §12 (catálogo erros).
/// </summary>
public sealed class ContractDtoTests
{
    // =========================================================================
    // Money como long (centavos) — nunca decimal
    // =========================================================================

    [Fact]
    public void OpportunityResponse_ValorTotal_DeveSer_Long()
    {
        // Red → Green: valor_total em centavos (long), nunca decimal (RNF 11.1)
        var prop = typeof(OpportunityResponse).GetProperty("ValorTotal");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            "valor_total deve ser long (centavos inteiros) conforme RNF 11.1 e DD-004");
    }

    [Fact]
    public void OpportunityResponse_ForecastPonderado_DeveSer_Long()
    {
        var prop = typeof(OpportunityResponse).GetProperty("ForecastPonderado");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            "forecast_ponderado deve ser long (centavos inteiros) conforme RNF 11.1");
    }

    [Fact]
    public void OpportunityResponse_ForecastLiquido_DeveSer_NullableLong()
    {
        var prop = typeof(OpportunityResponse).GetProperty("ForecastLiquido");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long?),
            "forecast_liquido deve ser long? (centavos, nullable quando sem comissão) conforme Req 13");
    }

    [Fact]
    public void KanbanColumnResponse_TotalValor_DeveSer_Long()
    {
        // KanbanResponse inclui total_valor e total_forecast por coluna em centavos (TASK-19 §critérios)
        var prop = typeof(KanbanColumnResponse).GetProperty("TotalValor");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            "total_valor deve ser long (centavos) conforme RNF 11.1");
    }

    [Fact]
    public void KanbanColumnResponse_TotalForecast_DeveSer_Long()
    {
        var prop = typeof(KanbanColumnResponse).GetProperty("TotalForecast");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            "total_forecast deve ser long (centavos) conforme RNF 11.1");
    }

    [Fact]
    public void CommissionDetailResponse_ComissaoTotal_DeveSer_Long()
    {
        var prop = typeof(CommissionDetailResponse).GetProperty("ComissaoTotal");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            "comissao_total deve ser long (centavos) conforme RNF 11.1");
    }

    [Fact]
    public void CommissionResponse_ForecastLiquido_DeveSer_Long()
    {
        var prop = typeof(CommissionResponse).GetProperty("ForecastLiquido");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long),
            "forecast_liquido em CommissionResponse deve ser long (centavos) conforme RNF 11.1");
    }

    [Fact]
    public void NenhumCampoDeMoneyNosResponses_DeveSer_Decimal()
    {
        // Verifica que não há propriedades decimal nos responses (exceto percentuais de taxa em CommissionDetail)
        var moneyFieldNames = new[]
        {
            "ValorTotal", "ValorSetup", "ValorMensal", "ValorFixo",
            "ForecastPonderado", "ForecastLiquido", "ComissaoTotal",
            "TotalValor", "TotalForecast"
        };

        var responseTypes = new[]
        {
            typeof(OpportunityResponse),
            typeof(OpportunitySummaryResponse),
            typeof(KanbanColumnResponse),
            typeof(CommissionResponse),
        };

        foreach (var type in responseTypes)
        {
            foreach (var fieldName in moneyFieldNames)
            {
                var prop = type.GetProperty(fieldName);
                if (prop is null) continue;

                prop.PropertyType.Should().NotBe(typeof(decimal),
                    $"{type.Name}.{fieldName} deve ser long (centavos), não decimal (RNF 11.1)");
                prop.PropertyType.Should().NotBe(typeof(decimal?),
                    $"{type.Name}.{fieldName} deve ser long? (centavos), não decimal? (RNF 11.1)");
            }
        }
    }

    // =========================================================================
    // Sem PII nos envelopes .v1 (Req 20.3, RNF 10.4)
    // =========================================================================

    private static readonly string[] PiiFieldNames =
    [
        "contact_name", "ContactName",
        "email", "Email",
        "phone", "Phone",
        "cpf", "Cpf",
        "name", "Name",
        "document", "Document"
    ];

    [Fact]
    public void OpportunityCreatedV1_NaoDeveTerCamposPII()
    {
        AssertNoPiiFields(typeof(OpportunityCreatedV1));
    }

    [Fact]
    public void OpportunityStageChangedV1_NaoDeveTerCamposPII()
    {
        AssertNoPiiFields(typeof(OpportunityStageChangedV1));
    }

    [Fact]
    public void OpportunityWonV1_NaoDeveTerCamposPII()
    {
        AssertNoPiiFields(typeof(OpportunityWonV1));
    }

    [Fact]
    public void OpportunityLostV1_NaoDeveTerCamposPII()
    {
        AssertNoPiiFields(typeof(OpportunityLostV1));
    }

    [Fact]
    public void OpportunityStaleV1_NaoDeveTerCamposPII()
    {
        AssertNoPiiFields(typeof(OpportunityStaleV1));
    }

    [Fact]
    public void OpportunityReopenedV1_NaoDeveTerCamposPII()
    {
        AssertNoPiiFields(typeof(OpportunityReopenedV1));
    }

    [Fact]
    public void CommissionCalculatedV1_NaoDeveTerCamposPII()
    {
        AssertNoPiiFields(typeof(CommissionCalculatedV1));
    }

    [Fact]
    public void CommissionSnapshotCreatedV1_NaoDeveTerCamposPII()
    {
        AssertNoPiiFields(typeof(CommissionSnapshotCreatedV1));
    }

    private static void AssertNoPiiFields(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            foreach (var piiField in PiiFieldNames)
            {
                prop.Name.ToLowerInvariant().Should().NotContain(piiField.ToLowerInvariant(),
                    $"evento {type.Name} não deve expor PII de contato (Req 20.3, RNF 10.4). Campo proibido: '{piiField}'");
            }
        }
    }

    // =========================================================================
    // Catálogo OP-ERR-001..017 — constantes tipadas
    // =========================================================================

    [Fact]
    public void ErrorCodes_DeveConterTodos_OP_ERR_001_A_017()
    {
        // Catálogo completo disponível como constantes estáticas (TASK-19 §critérios)
        var codes = new[]
        {
            ErrorCodes.OP_ERR_001, ErrorCodes.OP_ERR_002, ErrorCodes.OP_ERR_003,
            ErrorCodes.OP_ERR_004, ErrorCodes.OP_ERR_005, ErrorCodes.OP_ERR_006,
            ErrorCodes.OP_ERR_007, ErrorCodes.OP_ERR_008, ErrorCodes.OP_ERR_009,
            ErrorCodes.OP_ERR_010, ErrorCodes.OP_ERR_011, ErrorCodes.OP_ERR_012,
            ErrorCodes.OP_ERR_013, ErrorCodes.OP_ERR_014, ErrorCodes.OP_ERR_015,
            ErrorCodes.OP_ERR_016, ErrorCodes.OP_ERR_017
        };

        codes.Should().HaveCount(17, "catálogo deve ter exatamente OP-ERR-001..017");
        codes.Should().OnlyHaveUniqueItems("todos os códigos devem ser únicos");
        codes.Should().AllSatisfy(c => c.Should().StartWith("OP-ERR-", "todos os códigos seguem o padrão OP-ERR-NNN"));
    }

    [Fact]
    public void ErrorCodes_Messages_DeveConter_MensagemParaCadaCodigo()
    {
        var allCodes = new[]
        {
            ErrorCodes.OP_ERR_001, ErrorCodes.OP_ERR_002, ErrorCodes.OP_ERR_003,
            ErrorCodes.OP_ERR_004, ErrorCodes.OP_ERR_005, ErrorCodes.OP_ERR_006,
            ErrorCodes.OP_ERR_007, ErrorCodes.OP_ERR_008, ErrorCodes.OP_ERR_009,
            ErrorCodes.OP_ERR_010, ErrorCodes.OP_ERR_011, ErrorCodes.OP_ERR_012,
            ErrorCodes.OP_ERR_013, ErrorCodes.OP_ERR_014, ErrorCodes.OP_ERR_015,
            ErrorCodes.OP_ERR_016, ErrorCodes.OP_ERR_017
        };

        foreach (var code in allCodes)
        {
            ErrorCodes.Messages.Should().ContainKey(code,
                $"catálogo de mensagens deve ter entrada para '{code}'");
            ErrorCodes.Messages[code].Should().NotBeNullOrWhiteSpace(
                $"mensagem de '{code}' não pode ser vazia");
        }
    }

    [Theory]
    [InlineData("OP-ERR-002")]
    [InlineData("OP-ERR-008")]
    [InlineData("OP-ERR-013")]
    public void ErrorCodes_Codigos_DeveSerConstanteEstatica(string expectedCode)
    {
        // Verifica que os códigos são constantes estáticas (não computados em runtime)
        var fields = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(fi => fi.IsLiteral && !fi.IsInitOnly);

        fields.Select(f => f.GetValue(null) as string)
            .Should().Contain(expectedCode,
                $"'{expectedCode}' deve ser uma constante estática em ErrorCodes");
    }

    // =========================================================================
    // Envelopes .v1 — event_type e versão corretos
    // =========================================================================

    [Theory]
    [InlineData(typeof(OpportunityCreatedV1), "opportunity.created.v1")]
    [InlineData(typeof(OpportunityStageChangedV1), "opportunity.stage_changed.v1")]
    [InlineData(typeof(OpportunityWonV1), "opportunity.won.v1")]
    [InlineData(typeof(OpportunityLostV1), "opportunity.lost.v1")]
    [InlineData(typeof(OpportunityStaleV1), "opportunity.stale.v1")]
    [InlineData(typeof(OpportunityReopenedV1), "opportunity.reopened.v1")]
    [InlineData(typeof(CommissionCalculatedV1), "commission.calculated.v1")]
    [InlineData(typeof(CommissionSnapshotCreatedV1), "commission.snapshot_created.v1")]
    public void EventoV1_EventType_DeveCorresponderAoNomeConvencional(Type eventType, string expectedEventType)
    {
        // Instancia via reflection para verificar o EventType (abstract property)
        var tenantId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        // Cria uma instância mínima do evento para verificar EventType
        DomainEventEnvelope? envelope = eventType.Name switch
        {
            nameof(OpportunityCreatedV1) => new OpportunityCreatedV1
            {
                TenantId = tenantId,
                AggregateId = aggregateId,
                OccurredAt = occurredAt,
                OpportunityNumber = "AZ-0001",
                BuId = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                OwnerId = Guid.NewGuid(),
                StageId = Guid.NewGuid(),
                OriginChannelId = Guid.NewGuid(),
                ActorId = Guid.NewGuid()
            },
            nameof(OpportunityStageChangedV1) => new OpportunityStageChangedV1
            {
                TenantId = tenantId,
                AggregateId = aggregateId,
                OccurredAt = occurredAt,
                ToStageId = Guid.NewGuid(),
                ToCategory = "open",
                ActorId = Guid.NewGuid()
            },
            nameof(OpportunityWonV1) => new OpportunityWonV1
            {
                TenantId = tenantId,
                AggregateId = aggregateId,
                OccurredAt = occurredAt,
                ValorTotal = 100000L,
                ClosedAt = occurredAt,
                ActorId = Guid.NewGuid()
            },
            nameof(OpportunityLostV1) => new OpportunityLostV1
            {
                TenantId = tenantId,
                AggregateId = aggregateId,
                OccurredAt = occurredAt,
                LossReasonId = Guid.NewGuid(),
                ClosedAt = occurredAt,
                ActorId = Guid.NewGuid()
            },
            nameof(OpportunityStaleV1) => new OpportunityStaleV1
            {
                TenantId = tenantId,
                AggregateId = aggregateId,
                OccurredAt = occurredAt,
                BuId = Guid.NewGuid(),
                DetectedAt = occurredAt,
                DetectionPeriod = "2026-06-14"
            },
            nameof(OpportunityReopenedV1) => new OpportunityReopenedV1
            {
                TenantId = tenantId,
                AggregateId = aggregateId,
                OccurredAt = occurredAt,
                PreviousCategory = "won",
                ActorId = Guid.NewGuid()
            },
            nameof(CommissionCalculatedV1) => new CommissionCalculatedV1
            {
                TenantId = tenantId,
                AggregateId = aggregateId,
                OccurredAt = occurredAt,
                PartnerId = Guid.NewGuid(),
                ComissaoTotal = 50000L
            },
            nameof(CommissionSnapshotCreatedV1) => new CommissionSnapshotCreatedV1
            {
                TenantId = tenantId,
                AggregateId = aggregateId,
                OccurredAt = occurredAt,
                PartnerId = Guid.NewGuid(),
                CommissionId = Guid.NewGuid(),
                ComissaoTotal = 50000L,
                SnapshotAt = occurredAt
            },
            _ => throw new InvalidOperationException($"Tipo de evento não mapeado: {eventType.Name}")
        };

        envelope.EventType.Should().Be(expectedEventType,
            $"evento {eventType.Name} deve ter EventType = '{expectedEventType}'");
        envelope.Version.Should().Be("v1", "todos os envelopes públicos são .v1");
        envelope.AggregateType.Should().Be("Opportunity", "tipo do agregado é fixo");
    }

    // =========================================================================
    // OpportunityResponse contém forecast_liquido (TASK-19 §critérios)
    // =========================================================================

    [Fact]
    public void OpportunityResponse_DeveConterForecastLiquido()
    {
        var prop = typeof(OpportunityResponse).GetProperty("ForecastLiquido");
        prop.Should().NotBeNull("OpportunityResponse deve incluir forecast_liquido quando comissão presente");
        prop!.PropertyType.Should().Be(typeof(long?),
            "forecast_liquido é nullable (null quando sem comissão)");
    }
}
