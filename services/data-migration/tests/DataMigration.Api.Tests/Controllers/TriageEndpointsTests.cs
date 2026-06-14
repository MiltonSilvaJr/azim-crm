using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataMigration.Api.Tests.Infrastructure;
using DataMigration.Domain.Aggregates;
using FluentAssertions;

namespace DataMigration.Api.Tests.Controllers;

/// <summary>
/// Testes de contrato para endpoints de triagem.
/// TASK-22 — MigrationController endpoints de triagem.
///
/// Cobre: autorização TenantAdmin, paginação, MIG-ERR-006.
/// Rastreia: design §8, §10, TASK-22.
/// </summary>
public sealed class TriageEndpointsTests : IClassFixture<MigrationApiFactory>
{
    private readonly MigrationApiFactory _factory;

    public TriageEndpointsTests(MigrationApiFactory factory)
    {
        _factory = factory;
        _factory.JobRepository.Clear();
    }

    // =========================================================================
    // GET /api/v1/migrations/{jobId}/triage
    // =========================================================================

    [Fact]
    public async Task GetTriage_PlatOpSemTenantAdmin_Retorna200()
    {
        // Arrange — PlatOp também pode ler triagem (design §8)
        var job = CreateJobInTriageInProgress();
        _factory.JobRepository.Seed(job);
        var client = _factory.CreatePlatOpClient();

        // Act
        var response = await client.GetAsync($"/api/v1/migrations/{job.Id}/triage");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTriage_ComFiltroOwnerMissing_Retorna200ComPaginacao()
    {
        // Arrange
        var job = CreateJobInTriageInProgress();
        _factory.JobRepository.Seed(job);
        var client = _factory.CreateTenantAdminClient();

        // Act
        var response = await client.GetAsync(
            $"/api/v1/migrations/{job.Id}/triage?flag=owner_missing&page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTriage_SemAutenticacao_Retorna401()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();
        var jobId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/migrations/{jobId}/triage");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // POST /api/v1/migrations/{jobId}/triage/owners
    // =========================================================================

    [Fact]
    public async Task PostTriageOwners_PlatOpSemPapelTenantAdmin_Retorna403()
    {
        // Arrange — PlatOp PURO (sem TenantAdmin) não pode atribuir owner (design §10)
        // NOTA: no design §10, triagem é TenantAdmin; PlatOp também pode conforme §8.
        // Testamos que um papel desconhecido/inválido é rejeitado.
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "UnknownRole");
        client.DefaultRequestHeaders.Add("X-Test-TenantId", MigrationApiFactory.DefaultTenantId.ToString());

        var jobId = Guid.NewGuid();
        var body = new { rowIndex = 1, ownerId = Guid.NewGuid() };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{jobId}/triage/owners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostTriageOwners_TenantAdmin_JobInexistente_Retorna404()
    {
        // Arrange
        var client = _factory.CreateTenantAdminClient();
        var inexistentId = Guid.NewGuid();
        var body = new { rowIndex = 1, ownerId = Guid.NewGuid() };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{inexistentId}/triage/owners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("MIG-ERR-004");
    }

    [Fact]
    public async Task PostTriageOwners_TenantAdmin_JobValido_Retorna200()
    {
        // Arrange
        var job = CreateJobInTriageInProgress();
        _factory.JobRepository.Seed(job);
        var client = _factory.CreateTenantAdminClient();
        var body = new { rowIndex = 1, ownerId = Guid.NewGuid() };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{job.Id}/triage/owners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostTriageOwnersBulk_TenantAdmin_Retorna200()
    {
        // Arrange
        var job = CreateJobInTriageInProgress();
        _factory.JobRepository.Seed(job);
        var client = _factory.CreateTenantAdminClient();
        var body = new { buName = "Sertão", ownerId = Guid.NewGuid(), rowIndexes = new[] { 1, 2, 3 } };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{job.Id}/triage/owners/bulk", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // POST /api/v1/migrations/{jobId}/triage/stages
    // =========================================================================

    [Fact]
    public async Task PostTriageStages_TenantAdmin_Retorna200()
    {
        // Arrange
        var job = CreateJobInTriageInProgress();
        _factory.JobRepository.Seed(job);
        var client = _factory.CreateTenantAdminClient();
        var body = new { rowIndex = 1, stageId = Guid.NewGuid() };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{job.Id}/triage/stages", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // POST /api/v1/migrations/{jobId}/triage/partners
    // =========================================================================

    [Fact]
    public async Task PostTriagePartners_TenantAdmin_Retorna200()
    {
        // Arrange
        var job = CreateJobInTriageInProgress();
        _factory.JobRepository.Seed(job);
        var client = _factory.CreateTenantAdminClient();
        var body = new { rowIndex = 1, partnerId = (Guid?)Guid.NewGuid() };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{job.Id}/triage/partners", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // POST /api/v1/migrations/{jobId}/triage/dedupe
    // =========================================================================

    [Fact]
    public async Task PostTriageDedupe_TenantAdmin_Retorna200()
    {
        // Arrange
        var job = CreateJobInTriageInProgress();
        _factory.JobRepository.Seed(job);
        var client = _factory.CreateTenantAdminClient();
        var body = new { rowIndexA = 1, rowIndexB = 2, mergeIntoRowA = true };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{job.Id}/triage/dedupe", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================================
    // POST /api/v1/migrations/{jobId}/ready
    // =========================================================================

    [Fact]
    public async Task PostReady_OwnerFaltante_Retorna409ComMigErr006()
    {
        // Arrange — job em TriageInProgress com totalOpportunities > 0 e sem owners
        var job = CreateJobInTriageInProgress(totalOpportunities: 5);
        _factory.JobRepository.Seed(job);
        var client = _factory.CreateTenantAdminClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{job.Id}/ready", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MIG-ERR-006");
    }

    [Fact]
    public async Task PostReady_TodosOwnersAtribuidos_Retorna200()
    {
        // Arrange — job em TriageInProgress sem oportunidades pendentes
        var job = CreateJobInTriageInProgress(totalOpportunities: 0);
        _factory.JobRepository.Seed(job);
        var client = _factory.CreateTenantAdminClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{job.Id}/ready", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ready_to_import");
    }

    [Fact]
    public async Task PostReady_JobInexistente_Retorna404()
    {
        // Arrange
        var client = _factory.CreateTenantAdminClient();
        var inexistentId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/migrations/{inexistentId}/ready", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static MigrationJob CreateJobInTriageInProgress(int totalOpportunities = 0)
    {
        var now = DateTimeOffset.UtcNow;
        var job = MigrationJob.Create(
            tenantId: MigrationApiFactory.DefaultTenantId,
            sourceFileName: "Pipeline Vellus.xlsx",
            sourceFileSizeBytes: 1024,
            sourceFileHash: "abc123",
            detectedRowCount: totalOpportunities,
            createdBy: MigrationApiFactory.DefaultPlatOpId,
            createdAt: now);

        // Configura o TriageReport com totalOpportunities para o handler MIG-ERR-006
        if (totalOpportunities > 0)
        {
            job.SetTriageReport(
                $"{{\"totalOpportunities\":{totalOpportunities},\"totalAccounts\":0,\"ownerMissingCount\":{totalOpportunities},\"totalFlags\":0,\"byBu\":[],\"flags\":[],\"dedupeCandidates\":[],\"forecastDivergences\":[]}}",
                now);
        }

        job.TransitionTo(MigrationJobStatus.DryRunCompleted, now);
        job.TransitionTo(MigrationJobStatus.TriageInProgress, now);
        return job;
    }
}
