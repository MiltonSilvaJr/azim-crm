using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Requests;
using Reporting.Contracts.Responses;
using Xunit;

namespace Reporting.Api.Tests.Contracts;

/// <summary>
/// Testes de serialização JSON para os DTOs de Contracts (TASK-20).
///
/// Critérios cobertos:
/// <list type="bullet">
///   <item><description>Campos <c>*Cents</c> serializam como <c>long</c> (não string).</description></item>
///   <item><description><c>DisplayName</c> omitido no JSON quando <c>null</c>.</description></item>
///   <item><description><c>CsvExportResponse.SignedUrl</c> não pode ser <c>null</c> ou vazio.</description></item>
///   <item><description><c>PercentBasisPoints</c> serializa como inteiro.</description></item>
///   <item><description><c>Contracts</c> sem referência a <c>Domain</c>, <c>Application</c> ou <c>Infrastructure</c>.</description></item>
/// </list>
///
/// Mapeia: TASK-20, design §8.1, §8.3, DD-007.
/// </summary>
public sealed class ContractSerializationTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ─── FunnelRow ───────────────────────────────────────────────────────────

    [Fact]
    public void FunnelRow_TotalCents_SerializesAsLong()
    {
        var row = new FunnelRow(Guid.NewGuid(), "Proposta", "open", 5, 9_000_000L, 3_600_000L);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("totalCents").ValueKind.Should().Be(JsonValueKind.Number);
        doc.RootElement.GetProperty("totalCents").GetInt64().Should().Be(9_000_000L);
        doc.RootElement.GetProperty("weightedForecastCents").GetInt64().Should().Be(3_600_000L);
    }

    // ─── RankingRow ───────────────────────────────────────────────────────────

    [Fact]
    public void RankingRow_DisplayName_OmittedWhenNull()
    {
        var row = new RankingRow(Guid.NewGuid(), null, 3, 1_200_000L, 500_000L);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        // displayName NÃO deve aparecer no JSON quando null (DD-008, TASK-20)
        doc.RootElement.TryGetProperty("displayName", out _).Should().BeFalse(
            "displayName não deve ser serializado quando null (DD-008, RNF 4)");
    }

    [Fact]
    public void RankingRow_DisplayName_PresentWhenNotNull()
    {
        var row = new RankingRow(Guid.NewGuid(), "João Silva", 3, 1_200_000L, 500_000L);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("displayName", out var prop).Should().BeTrue();
        prop.GetString().Should().Be("João Silva");
    }

    [Fact]
    public void RankingRow_MonetaryFields_SerializeAsLong()
    {
        var row = new RankingRow(Guid.NewGuid(), null, 10, 5_000_000L, 2_000_000L);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("wonValueCents").ValueKind.Should().Be(JsonValueKind.Number);
        doc.RootElement.GetProperty("pipelineForecastCents").ValueKind.Should().Be(JsonValueKind.Number);
        doc.RootElement.GetProperty("wonValueCents").GetInt64().Should().Be(5_000_000L);
    }

    // ─── ChannelRow ───────────────────────────────────────────────────────────

    [Fact]
    public void ChannelRow_PercentBasisPoints_SerializesAsInteger()
    {
        var row = new ChannelRow(Guid.NewGuid(), "Indicação", 10, 3_000_000L, 5000);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("percentBasisPoints").ValueKind.Should().Be(JsonValueKind.Number);
        doc.RootElement.GetProperty("percentBasisPoints").GetInt32().Should().Be(5000);
    }

    [Fact]
    public void ChannelRow_TotalCents_SerializesAsLong()
    {
        var row = new ChannelRow(Guid.NewGuid(), "Direto", 7, 2_500_000L, 3000);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("totalCents").GetInt64().Should().Be(2_500_000L);
    }

    // ─── ForecastRow ─────────────────────────────────────────────────────────

    [Fact]
    public void ForecastRow_GoalCents_OmittedWhenNull()
    {
        var row = new ForecastRow(Guid.NewGuid(), "BU Norte", 2026, 6, 8_000_000L, 3_000_000L, null);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("goalCents", out _).Should().BeFalse(
            "goalCents deve ser omitido no JSON quando null (degradação graciosa Req 6.3)");
    }

    [Fact]
    public void ForecastRow_GoalCents_PresentWhenNotNull()
    {
        var row = new ForecastRow(Guid.NewGuid(), "BU Sul", 2026, 5, 6_000_000L, 2_000_000L, 10_000_000L);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("goalCents", out var prop).Should().BeTrue();
        prop.GetInt64().Should().Be(10_000_000L);
    }

    // ─── CommissionPartnerRow ─────────────────────────────────────────────────

    [Fact]
    public void CommissionPartnerRow_MonetaryFields_SerializeAsLong()
    {
        var row = new CommissionPartnerRow(Guid.NewGuid(), "Parceiro A", 1_500_000L, 800_000L, 5);
        var json = JsonSerializer.Serialize(row, _options);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("projectedCents").GetInt64().Should().Be(1_500_000L);
        doc.RootElement.GetProperty("consolidatedCents").GetInt64().Should().Be(800_000L);
    }

    // ─── CsvExportResponse ────────────────────────────────────────────────────

    [Fact]
    public void CsvExportResponse_SignedUrl_MustNotBeNullOrEmpty()
    {
        var response = new CsvExportResponse(
            "funnel",
            "funil_2026-01_2026-06.csv",
            "https://storage.googleapis.com/azim-reports/...",
            DateTimeOffset.UtcNow.AddMinutes(15));

        response.SignedUrl.Should().NotBeNullOrEmpty(
            "signedUrl é obrigatório — sem ele o cliente não consegue baixar o CSV (DD-004)");
    }

    [Fact]
    public void CsvExportResponse_ContainsRequiredFields()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var response = new CsvExportResponse(
            "commissions",
            "comissoes_2026-01_2026-06.csv",
            "https://storage.googleapis.com/azim-reports/tenant/commissions/hash.csv?X-Goog-Expires=900",
            expiresAt);

        var json = JsonSerializer.Serialize(response, _options);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("reportType", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("filename", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("signedUrl", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("expiresAt", out _).Should().BeTrue();
    }

    // ─── ReportFilterRequest ─────────────────────────────────────────────────

    [Fact]
    public void ReportFilterRequest_CanBeConstructedWithDefaults()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 6, 30);
        var request = new ReportFilterRequest(from, to, null);

        request.From.Should().Be(from);
        request.To.Should().Be(to);
        request.BuIds.Should().BeNull();
    }

    [Fact]
    public void ReportFilterRequest_BuIds_AcceptsMultiple()
    {
        var bu1 = Guid.NewGuid();
        var bu2 = Guid.NewGuid();
        var request = new ReportFilterRequest(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31),
            [bu1, bu2]);

        request.BuIds.Should().HaveCount(2).And.Contain(bu1).And.Contain(bu2);
    }

    // ─── Contracts independência de Domain ───────────────────────────────────

    [Fact]
    public void ContractsAssembly_HasNoDependencyOnDomain()
    {
        // Contracts → ∅ (design §3)
        var contractsAssembly = typeof(FunnelRow).Assembly;
        var referencedNames = contractsAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        referencedNames.Should().NotContain(
            name => name.Contains("Reporting.Domain") ||
                    name.Contains("Reporting.Application") ||
                    name.Contains("Reporting.Infrastructure"),
            "Contracts não pode depender de Domain, Application ou Infrastructure (design §3)");
    }
}
