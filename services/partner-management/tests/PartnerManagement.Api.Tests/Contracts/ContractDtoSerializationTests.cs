using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAssertions;
using PartnerManagement.Contracts.Partners;
using PartnerManagement.Contracts.Commissions;
using PartnerManagement.Contracts.Events;
using Xunit;

namespace PartnerManagement.Api.Tests.Contracts;

/// <summary>
/// Testes de serialização/deserialização e validação de anotações dos DTOs de contratos.
/// TDD-first: ST-01 — Red (escritos antes da implementação dos DTOs).
/// Mapeia: TASK-22, design §8, design §9.
/// </summary>
public sealed class ContractDtoSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // =========================================================================
    // CreatePartnerRequest
    // =========================================================================

    [Fact]
    public void CreatePartnerRequest_ShouldSerializeAndDeserialize_RoundTrip()
    {
        var dto = new CreatePartnerRequest
        {
            Name = "Acme Ltda",
            Role = "Indicador",
            PctSetup = 10.50m,
            PctRecorrente = 5.25m,
            ContactEmail = "contato@acme.com",
            ContactPhone = "11999990000",
            Notes = "Parceiro teste",
            ConfirmCreateDespiteDuplicate = false
        };

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        CreatePartnerRequest? deserialized = JsonSerializer.Deserialize<CreatePartnerRequest>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("Acme Ltda");
        deserialized.Role.Should().Be("Indicador");
        deserialized.PctSetup.Should().Be(10.50m);
        deserialized.PctRecorrente.Should().Be(5.25m);
        deserialized.ConfirmCreateDespiteDuplicate.Should().BeFalse();
    }

    [Fact]
    public void CreatePartnerRequest_WithNullName_ShouldFailValidation()
    {
        var dto = new CreatePartnerRequest { Name = null!, Role = "Indicador" };
        var results = new List<ValidationResult>();
        bool valid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        valid.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CreatePartnerRequest.Name)));
    }

    [Fact]
    public void CreatePartnerRequest_WithNullRole_ShouldFailValidation()
    {
        var dto = new CreatePartnerRequest { Name = "Acme", Role = null! };
        var results = new List<ValidationResult>();
        bool valid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        valid.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CreatePartnerRequest.Role)));
    }

    [Fact]
    public void CreatePartnerRequest_PctSetup_ShouldUseDecimalNotFloat()
    {
        var dto = new CreatePartnerRequest { Name = "X", Role = "Y", PctSetup = 99.99m, PctRecorrente = 0.01m };
        // verifica que os campos são do tipo decimal (não float/double)
        typeof(CreatePartnerRequest).GetProperty(nameof(CreatePartnerRequest.PctSetup))!.PropertyType
            .Should().Be(typeof(decimal));
        typeof(CreatePartnerRequest).GetProperty(nameof(CreatePartnerRequest.PctRecorrente))!.PropertyType
            .Should().Be(typeof(decimal));
    }

    // =========================================================================
    // UpdatePartnerRequest
    // =========================================================================

    [Fact]
    public void UpdatePartnerRequest_ShouldSerializeAndDeserialize_RoundTrip()
    {
        var dto = new UpdatePartnerRequest
        {
            Name = "Acme Editado",
            Role = "Revendedor",
            PctSetup = 20.00m,
            PctRecorrente = 10.00m
        };

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        UpdatePartnerRequest? deserialized = JsonSerializer.Deserialize<UpdatePartnerRequest>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("Acme Editado");
        deserialized.Role.Should().Be("Revendedor");
    }

    // =========================================================================
    // PartnerResponse
    // =========================================================================

    [Fact]
    public void PartnerResponse_ShouldSerializeAllFields()
    {
        var dto = new PartnerResponse
        {
            PartnerId = Guid.NewGuid(),
            Name = "Acme",
            Role = "Indicador",
            PctSetup = 10.00m,
            PctRecorrente = 5.00m,
            Active = true,
            IsTriagePending = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        PartnerResponse? deserialized = JsonSerializer.Deserialize<PartnerResponse>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.PartnerId.Should().Be(dto.PartnerId);
        deserialized.Active.Should().BeTrue();
        deserialized.IsTriagePending.Should().BeFalse();
    }

    // =========================================================================
    // PartnerEligibilityResponse
    // =========================================================================

    [Fact]
    public void PartnerEligibilityResponse_ShouldSerializePartnerIdAndActive()
    {
        var id = Guid.NewGuid();
        var dto = new PartnerEligibilityResponse { PartnerId = id, Active = false };

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        PartnerEligibilityResponse? deserialized = JsonSerializer.Deserialize<PartnerEligibilityResponse>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.PartnerId.Should().Be(id);
        deserialized.Active.Should().BeFalse();
    }

    // =========================================================================
    // CommissionViewResponse
    // =========================================================================

    [Fact]
    public void CommissionViewResponse_ShouldUseIntegerCentsForCommission()
    {
        var dto = new CommissionViewResponse
        {
            PartnerId = Guid.NewGuid(),
            PeriodFrom = DateTimeOffset.UtcNow.AddDays(-30),
            PeriodTo = DateTimeOffset.UtcNow,
            ProjectedCommissionCents = 150000L,
            ConsolidatedCommissionCents = 300000L,
            CommissionUnavailable = false
        };

        // valores monetários como long (centavos inteiros — money-as-cents)
        typeof(CommissionViewResponse).GetProperty(nameof(CommissionViewResponse.ProjectedCommissionCents))!.PropertyType
            .Should().Be(typeof(long));
        typeof(CommissionViewResponse).GetProperty(nameof(CommissionViewResponse.ConsolidatedCommissionCents))!.PropertyType
            .Should().Be(typeof(long));

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        CommissionViewResponse? deserialized = JsonSerializer.Deserialize<CommissionViewResponse>(json, JsonOptions);

        deserialized!.ProjectedCommissionCents.Should().Be(150000L);
        deserialized.ConsolidatedCommissionCents.Should().Be(300000L);
    }

    [Fact]
    public void CommissionViewResponse_WhenUnavailable_ShouldSerializeFlag()
    {
        var dto = new CommissionViewResponse
        {
            PartnerId = Guid.NewGuid(),
            PeriodFrom = DateTimeOffset.UtcNow.AddDays(-30),
            PeriodTo = DateTimeOffset.UtcNow,
            CommissionUnavailable = true
        };

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        CommissionViewResponse? deserialized = JsonSerializer.Deserialize<CommissionViewResponse>(json, JsonOptions);
        deserialized!.CommissionUnavailable.Should().BeTrue();
    }

    // =========================================================================
    // CommissionReportResponse
    // =========================================================================

    [Fact]
    public void CommissionReportResponse_ShouldSerializeLines()
    {
        var dto = new CommissionReportResponse
        {
            PartnerId = Guid.NewGuid(),
            PeriodFrom = DateTimeOffset.UtcNow.AddDays(-30),
            PeriodTo = DateTimeOffset.UtcNow,
            Lines =
            [
                new CommissionReportLine
                {
                    OpportunityId = Guid.NewGuid(),
                    CommissionCents = 50000L,
                    IsSnapshot = false,
                    OccurredAt = DateTimeOffset.UtcNow
                }
            ]
        };

        string json = JsonSerializer.Serialize(dto, JsonOptions);
        CommissionReportResponse? deserialized = JsonSerializer.Deserialize<CommissionReportResponse>(json, JsonOptions);

        deserialized!.Lines.Should().HaveCount(1);
        // CommissionCents deve ser long (centavos inteiros — money-as-cents)
        typeof(CommissionReportLine).GetProperty(nameof(CommissionReportLine.CommissionCents))!.PropertyType
            .Should().Be(typeof(long));
    }

    // =========================================================================
    // Envelopes de evento — sem PII em claro (RNF 4)
    // =========================================================================

    [Fact]
    public void PartnerCreatedV1_ShouldNotContainNameOrContact()
    {
        var evt = new PartnerCreatedV1
        {
            EventId = Guid.NewGuid(),
            EventVersion = "v1",
            PartnerId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            PartnerType = "Indicador",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = "corr-001"
        };

        string json = JsonSerializer.Serialize(evt, JsonOptions);

        // payload não deve conter campos de PII
        json.Should().NotContain("name");
        json.Should().NotContain("email");
        json.Should().NotContain("phone");
        json.Should().NotContain("contact");
    }

    [Fact]
    public void PartnerCommissionPercentagesUpdatedV1_ShouldNotContainPii()
    {
        var evt = new PartnerCommissionPercentagesUpdatedV1
        {
            EventId = Guid.NewGuid(),
            EventVersion = "v1",
            PartnerId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ChangedFields = ["pct_setup", "pct_recorrente"],
            OccurredAt = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(evt, JsonOptions);
        json.Should().NotContain("name");
        json.Should().NotContain("email");
    }

    [Fact]
    public void PartnerDeactivatedV1_ShouldContainPartnerIdAndTenantId()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var evt = new PartnerDeactivatedV1
        {
            EventId = Guid.NewGuid(),
            EventVersion = "v1",
            PartnerId = partnerId,
            TenantId = tenantId,
            OccurredAt = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(evt, JsonOptions);
        PartnerDeactivatedV1? deserialized = JsonSerializer.Deserialize<PartnerDeactivatedV1>(json, JsonOptions);

        deserialized!.PartnerId.Should().Be(partnerId);
        deserialized.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void PartnerReactivatedV1_ShouldSerialize()
    {
        var evt = new PartnerReactivatedV1
        {
            EventId = Guid.NewGuid(),
            EventVersion = "v1",
            PartnerId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(evt, JsonOptions);
        PartnerReactivatedV1? deserialized = JsonSerializer.Deserialize<PartnerReactivatedV1>(json, JsonOptions);
        deserialized.Should().NotBeNull();
    }
}
