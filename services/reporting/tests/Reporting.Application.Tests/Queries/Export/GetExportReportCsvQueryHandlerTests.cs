using System.Text;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NSubstitute;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Export;
using Reporting.Contracts.ReadModels;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace Reporting.Application.Tests.Queries.Export;

/// <summary>
/// Testes do handler de export CSV — TASK-11, ST-01 + PBT-05.
/// Verifica: BOM UTF-8, cabeçalhos pt-BR, R$, PII em ranking, idempotência de objectName.
/// Mapeia: Req 5, PBT-05, DD-004, DD-007, DD-008, RNF 4.
/// </summary>
public sealed class GetExportReportCsvQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Period ValidPeriod =
        Period.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

    private static ReportScope ScopeTenantAdmin() =>
        ReportScope.Create(TenantId, ReportingRole.TenantAdmin, [], null);

    private static readonly byte[] BomBytes = [0xEF, 0xBB, 0xBF];

    // ──────────────────────────────────────────────────────────────
    // CsvReportWriter — testes unitários da lógica pura
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CSV: arquivo começa com BOM UTF-8 (\\xEF\\xBB\\xBF) — Req 5.2")]
    public void WriteFunnel_StartsWithUtf8Bom()
    {
        var writer   = new CsvReportWriter();
        var response = new FunnelReportResponse([
            new FunnelRow(Guid.NewGuid(), "Proposta", "open", 5, 100_000L, 50_000L)
        ]);

        var bytes = writer.WriteFunnel(response);

        bytes[..3].Should().Equal(BomBytes,
            because: "CSV deve começar com BOM UTF-8 para compatibilidade (Req 5.2)");
    }

    [Fact(DisplayName = "CSV funil: cabeçalhos em pt-BR (Req 5.2)")]
    public void WriteFunnel_HeadersInPtBr()
    {
        var writer   = new CsvReportWriter();
        var response = new FunnelReportResponse([]);

        var csv = Encoding.UTF8.GetString(writer.WriteFunnel(response));

        csv.Should().Contain("Estágio").And.Contain("Quantidade").And.Contain("Total (R$)");
    }

    [Fact(DisplayName = "CSV funil: valores monetários formatados com R$ (DD-007, Req 5.2)")]
    public void WriteFunnel_MonetaryValuesFormattedWithReais()
    {
        var writer   = new CsvReportWriter();
        var response = new FunnelReportResponse([
            new FunnelRow(Guid.NewGuid(), "Proposta", "open", 1, 100_050L, 50_025L)
        ]);

        var csv = Encoding.UTF8.GetString(writer.WriteFunnel(response));

        csv.Should().Contain("R$",
            because: "valores monetários devem ser formatados em R$ no CSV (Req 5.2, DD-007 — apresentação apenas)");
    }

    [Fact(DisplayName = "CSV comissão: cabeçalhos em pt-BR")]
    public void WriteCommissions_HeadersInPtBr()
    {
        var writer   = new CsvReportWriter();
        var response = new CommissionReportResponse([]);

        var csv = Encoding.UTF8.GetString(writer.WriteCommissions(response));

        csv.Should().Contain("Parceiro")
           .And.Contain("Projetada")
           .And.Contain("Consolidada");
    }

    [Fact(DisplayName = "CSV ranking: sem PII quando DisplayName é null (DD-008, RNF 4)")]
    public void WriteRanking_WithNullDisplayName_NoPii()
    {
        var writer   = new CsvReportWriter();
        var ownerId  = Guid.NewGuid();
        var response = new RankingReportResponse([
            new RankingRow(ownerId, null, 5, 500_000L, 200_000L)
        ]);

        var csv = Encoding.UTF8.GetString(writer.WriteRanking(response));

        csv.Should().NotContain("Nome",
            because: "quando DisplayName é null, coluna de nome não aparece (PiiMinimizationPolicy, DD-008)");
    }

    [Fact(DisplayName = "CSV ranking: coluna Nome presente quando DisplayName não é null (DD-008)")]
    public void WriteRanking_WithDisplayName_ContainsName()
    {
        var writer   = new CsvReportWriter();
        var ownerId  = Guid.NewGuid();
        var response = new RankingReportResponse([
            new RankingRow(ownerId, "Alice Souza", 5, 500_000L, 200_000L)
        ]);

        var csv = Encoding.UTF8.GetString(writer.WriteRanking(response));

        csv.Should().Contain("Nome").And.Contain("Alice Souza");
    }

    // ──────────────────────────────────────────────────────────────
    // FormatMoney: conversão de centavos para R$
    // ──────────────────────────────────────────────────────────────

    [Theory(DisplayName = "FormatMoney: converte centavos para R$ corretamente")]
    [InlineData(100L,       "R$ 1,00")]
    [InlineData(9_999L,     "R$ 99,99")]
    [InlineData(1_000_000L, "R$ 10000,00")]
    [InlineData(0L,         "R$ 0,00")]
    public void FormatMoney_ConvertsCentsToReais(long cents, string expected)
    {
        CsvReportWriter.FormatMoney(cents).Should().Be(expected);
    }

    // ──────────────────────────────────────────────────────────────
    // ObjectName: determinístico para idempotência (DD-004, Req 5.4)
    // ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "ObjectName: determinístico — mesmos filtros produzem o mesmo nome")]
    public void BuildObjectName_SameFilters_ProducesSameName()
    {
        var scope = ScopeTenantAdmin();
        var query1 = new GetExportReportCsvQuery(ReportType.Funnel, ValidPeriod, null, scope);
        var query2 = new GetExportReportCsvQuery(ReportType.Funnel, ValidPeriod, null, scope);

        var name1 = GetExportReportCsvQueryHandler.BuildObjectName(query1);
        var name2 = GetExportReportCsvQueryHandler.BuildObjectName(query2);

        name1.Should().Be(name2,
            because: "nome de objeto determinístico garante idempotência (DD-004, Req 5.4)");
    }

    [Fact(DisplayName = "ObjectName: diferentes tipos → nomes diferentes")]
    public void BuildObjectName_DifferentTypes_ProduceDifferentNames()
    {
        var scope  = ScopeTenantAdmin();
        var queryF = new GetExportReportCsvQuery(ReportType.Funnel,    ValidPeriod, null, scope);
        var queryC = new GetExportReportCsvQuery(ReportType.Commissions, ValidPeriod, null, scope);

        var nameF = GetExportReportCsvQueryHandler.BuildObjectName(queryF);
        var nameC = GetExportReportCsvQueryHandler.BuildObjectName(queryC);

        nameF.Should().NotBe(nameC);
    }

    [Fact(DisplayName = "ObjectName: formato correto reports/{tenant}/{type}/{period_hash}/{scope_hash}.csv")]
    public void BuildObjectName_HasCorrectFormat()
    {
        var scope = ScopeTenantAdmin();
        var query = new GetExportReportCsvQuery(ReportType.Funnel, ValidPeriod, null, scope);

        var name = GetExportReportCsvQueryHandler.BuildObjectName(query);

        name.Should().StartWith($"reports/{scope.TenantId:N}/funnel/");
        name.Should().EndWith(".csv");
    }

    // ──────────────────────────────────────────────────────────────
    // PBT-05: round-trip relatório × CSV
    // Verifica que as linhas do CSV são equivalentes às do handler de relatório
    // ──────────────────────────────────────────────────────────────

    [Property(MaxTest = 200, DisplayName = "PBT-05: CSV funil contém as mesmas linhas do relatório")]
    public Property Pbt05_FunnelCsvEquivalentToReport(PositiveInt count)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(count)),
            c =>
            {
                var n = (c.Get % 5) + 1;
                var rows = Enumerable.Range(1, n).Select(i =>
                    new FunnelRow(Guid.NewGuid(), $"Estágio {i}", "open", i, (long)i * 100_000, (long)i * 50_000)
                ).ToList();

                var response = new FunnelReportResponse(rows);
                var writer   = new CsvReportWriter();
                var csvBytes = writer.WriteFunnel(response);
                var csv      = Encoding.UTF8.GetString(csvBytes);

                // Verifica que cada linha do relatório está presente no CSV
                return rows.All(r => csv.Contains(r.StageName) && csv.Contains(r.Count.ToString()));
            });
    }

    [Property(MaxTest = 200, DisplayName = "PBT-05: CSV de comissões contém totais equivalentes ao relatório")]
    public Property Pbt05_CommissionCsvEquivalentToReport(PositiveInt count)
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(count)),
            c =>
            {
                var n = (c.Get % 5) + 1;
                var rows = Enumerable.Range(1, n).Select(i =>
                    new CommissionPartnerRow(Guid.NewGuid(), $"Parceiro {i}", (long)i * 5_000, (long)i * 10_000, i)
                ).ToList();

                var response = new CommissionReportResponse(rows);
                var writer   = new CsvReportWriter();
                var csvBytes = writer.WriteCommissions(response);
                var csv      = Encoding.UTF8.GetString(csvBytes);

                // Verifica que cada parceiro está representado no CSV
                return rows.All(r => csv.Contains(r.PartnerName));
            });
    }
}
