using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DataMigration.Api.Tests.Infrastructure;
using DataMigration.Domain.Aggregates;
using FluentAssertions;

namespace DataMigration.Api.Tests.Controllers;

/// <summary>
/// Testes de contrato para endpoints de upload, dry-run e status.
/// TASK-21 — MigrationController.
///
/// Cobre: MIG-ERR-001..004 + happy paths + autenticação.
/// Rastreia: design §8, §10, §12, TASK-21.
/// </summary>
public sealed class UploadDryRunStatusTests : IClassFixture<MigrationApiFactory>
{
    private readonly MigrationApiFactory _factory;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public UploadDryRunStatusTests(MigrationApiFactory factory)
    {
        _factory = factory;
        _factory.JobRepository.Clear();
        _factory.FeatureFlags.Set("migration.import_enabled", true);
    }

    // =========================================================================
    // POST /api/v1/migrations/upload
    // =========================================================================

    [Fact]
    public async Task Upload_SemAutenticacao_Retorna401()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();
        using var content = BuildMultipartContent("arquivo.xlsx", [0x50, 0x4B, 0x03, 0x04]);

        // Act
        var response = await client.PostAsync("/api/v1/migrations/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_ArquivoNaoXlsx_Retorna422ComMigErr001()
    {
        // Arrange
        var client = _factory.CreatePlatOpClient();
        using var content = BuildMultipartContent("arquivo.csv", [0x01, 0x02]);

        // Act
        var response = await client.PostAsync("/api/v1/migrations/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MIG-ERR-001");
    }

    [Fact]
    public async Task Upload_ColunasAusentes_Retorna422ComMigErr002()
    {
        // Arrange — arquivo válido (.xlsx bytes mágicos) mas com conteúdo inválido
        // que não tem as colunas esperadas.
        var client = _factory.CreatePlatOpClient();
        using var content = BuildMultipartContent("pipeline.xlsx", CreateMinimalXlsx());

        // Act
        var response = await client.PostAsync("/api/v1/migrations/upload", content);

        // Assert — arquivo xlsx minimalista sem colunas canônicas → MIG-ERR-002
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MIG-ERR-002");
    }

    [Fact]
    public async Task Upload_TenantAdminSemPlatOp_Retorna403()
    {
        // Arrange — TenantAdmin não tem permissão para upload (design §10)
        var client = _factory.CreateTenantAdminClient();
        using var content = BuildMultipartContent("pipeline.xlsx", [0x50, 0x4B, 0x03, 0x04]);

        // Act
        var response = await client.PostAsync("/api/v1/migrations/upload", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // =========================================================================
    // POST /api/v1/migrations/{jobId}/dry-run
    // =========================================================================

    [Fact]
    public async Task DryRun_JobIdInexistente_Retorna404ComMigErr004()
    {
        // Arrange
        var client = _factory.CreatePlatOpClient();
        var inexistentId = Guid.NewGuid();
        using var content = BuildMultipartContent("pipeline.xlsx", CreateMinimalXlsx());

        // Act
        var response = await client.PostAsync($"/api/v1/migrations/{inexistentId}/dry-run", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MIG-ERR-004");
    }

    [Fact]
    public async Task DryRun_JobExistente_Retorna200ComTriageReport()
    {
        // Arrange — injeta job em Created no repositório
        var job = CreateJobInStatus(MigrationJobStatus.Created);
        _factory.JobRepository.Seed(job);

        var client = _factory.CreatePlatOpClient();
        using var content = BuildMultipartContent("pipeline.xlsx", CreateMinimalXlsx());

        // Act
        var response = await client.PostAsync($"/api/v1/migrations/{job.Id}/dry-run", content);

        // Assert — dry-run executa e retorna relatório de triagem
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DryRun_SemAutenticacao_Retorna401()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();
        var jobId = Guid.NewGuid();
        using var content = BuildMultipartContent("pipeline.xlsx", CreateMinimalXlsx());

        // Act
        var response = await client.PostAsync($"/api/v1/migrations/{jobId}/dry-run", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // GET /api/v1/migrations/{jobId}/status
    // =========================================================================

    [Fact]
    public async Task Status_JobIdInexistente_Retorna404ComMigErr004()
    {
        // Arrange
        var client = _factory.CreatePlatOpClient();
        var inexistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/migrations/{inexistentId}/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MIG-ERR-004");
    }

    [Fact]
    public async Task Status_JobExistente_Retorna200ComStatus()
    {
        // Arrange — injeta job em Created
        var job = CreateJobInStatus(MigrationJobStatus.Created);
        _factory.JobRepository.Seed(job);

        var client = _factory.CreatePlatOpClient();

        // Act
        var response = await client.GetAsync($"/api/v1/migrations/{job.Id}/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("created");
        body.Should().Contain(job.Id.ToString());
    }

    [Fact]
    public async Task Status_AcessivelPorTenantAdmin()
    {
        // Arrange — TenantAdmin pode consultar status (design §8)
        var job = CreateJobInStatus(MigrationJobStatus.Created);
        _factory.JobRepository.Seed(job);

        var client = _factory.CreateTenantAdminClient();

        // Act
        var response = await client.GetAsync($"/api/v1/migrations/{job.Id}/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Status_SemAutenticacao_Retorna401()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();
        var jobId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/migrations/{jobId}/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Status_RespostaSemPii()
    {
        // Arrange — job com nome de arquivo que não deve vazar PII
        var job = CreateJobInStatus(MigrationJobStatus.Created);
        _factory.JobRepository.Seed(job);

        var client = _factory.CreatePlatOpClient();

        // Act
        var response = await client.GetAsync($"/api/v1/migrations/{job.Id}/status");
        var body = await response.Content.ReadAsStringAsync();

        // Assert — resposta não deve conter campos sensíveis além do jobId e status
        body.Should().NotContain("email");
        body.Should().NotContain("phone");
        body.Should().NotContain("contactName");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static MultipartFormDataContent BuildMultipartContent(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private static MigrationJob CreateJobInStatus(MigrationJobStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var job = MigrationJob.Create(
            tenantId: MigrationApiFactory.DefaultTenantId,
            sourceFileName: "Pipeline Vellus.xlsx",
            sourceFileSizeBytes: 1024,
            sourceFileHash: "abc123",
            detectedRowCount: 10,
            createdBy: MigrationApiFactory.DefaultPlatOpId,
            createdAt: now);

        if (status == MigrationJobStatus.DryRunCompleted)
        {
            job.TransitionTo(MigrationJobStatus.DryRunCompleted, now);
        }
        else if (status == MigrationJobStatus.ReadyToImport)
        {
            job.TransitionTo(MigrationJobStatus.DryRunCompleted, now);
            job.TransitionTo(MigrationJobStatus.TriageInProgress, now);
            job.TransitionTo(MigrationJobStatus.ReadyToImport, now);
        }

        return job;
    }

    /// <summary>
    /// Cria bytes de um xlsx mínimo e válido (ZIP/OpenXML) mas sem as colunas
    /// canônicas esperadas → provoca MIG-ERR-002.
    /// </summary>
    private static byte[] CreateMinimalXlsx()
    {
        // Bytes mágicos do ZIP (PK header) — ClosedXML consegue abrir mas sem conteúdo útil.
        // Será detectado como .xlsx mas sem colunas → MIG-ERR-002.
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            // Estrutura mínima para que o ClosedXML reconheça como xlsx válido
            var contentTypes = archive.CreateEntry("[Content_Types].xml");
            using (var sw = new StreamWriter(contentTypes.Open()))
            {
                sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
</Types>");
            }

            var rels = archive.CreateEntry("_rels/.rels");
            using (var sw = new StreamWriter(rels.Open()))
            {
                sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");
            }

            var xlRels = archive.CreateEntry("xl/_rels/workbook.xml.rels");
            using (var sw = new StreamWriter(xlRels.Open()))
            {
                sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
</Relationships>");
            }

            var workbook = archive.CreateEntry("xl/workbook.xml");
            using (var sw = new StreamWriter(workbook.Open()))
            {
                sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""
          xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""Pipeline"" sheetId=""1"" r:id=""rId1""/>
  </sheets>
</workbook>");
            }

            var sheet = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using (var sw = new StreamWriter(sheet.Open()))
            {
                // Aba Pipeline sem as colunas canônicas esperadas
                sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <sheetData>
    <row r=""1"">
      <c r=""A1"" t=""inlineStr""><is><t>ColunaNaoReconhecida</t></is></c>
    </row>
  </sheetData>
</worksheet>");
            }
        }

        return ms.ToArray();
    }
}
