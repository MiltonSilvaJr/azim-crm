using DataMigration.Application.Ports;
using DataMigration.Infrastructure.Adapters;
using FluentAssertions;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Adapters;

/// <summary>
/// Testes unitários de <see cref="ActivityImportAdapter"/> e <see cref="OrganizationReadAdapter"/>.
///
/// Cobre:
/// - Criação de atividade com OpportunityId resolvido.
/// - Idempotência por import_key (DD-003).
/// - OrganizationReadAdapter: BU configurada → ID retornado; BU ausente → null.
/// - OrganizationReadAdapter: estágio e usuário análogos.
///
/// Rastreia: TASK-18, ST-01, DD-001, DD-003, Req 10, DEP-04, MIG-ERR-010.
/// </summary>
public sealed class ActivityOrganizationAdapterTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _opportunityId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    // =========================================================================
    // ActivityImportAdapter
    // =========================================================================

    [Fact]
    public async Task CreateOrUpdateAsync_AtividadeNova_RetornaIsNewTrue()
    {
        // Arrange
        var adapter = new ActivityImportAdapter();
        var request = BuildActivityRequest("key-act-001");

        // Act
        var result = await adapter.CreateOrUpdateAsync(request);

        // Assert
        result.IsNew.Should().BeTrue();
        result.ActivityId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_MesmaImportKey_RetornaMesmoId()
    {
        // Arrange — idempotência por import_key (DD-003).
        var adapter = new ActivityImportAdapter();
        var request = BuildActivityRequest("key-act-idem");

        // Act
        var result1 = await adapter.CreateOrUpdateAsync(request);
        var result2 = await adapter.CreateOrUpdateAsync(request);

        // Assert
        result2.ActivityId.Should().Be(result1.ActivityId, "mesma import_key → mesma atividade (DD-003)");
        result2.IsNew.Should().BeFalse();
    }

    [Fact]
    public async Task CreateOrUpdateAsync_ImportKeysDiferentes_RetornaAtividadesDiferentes()
    {
        // Arrange
        var adapter = new ActivityImportAdapter();
        var request1 = BuildActivityRequest("key-act-A");
        var request2 = BuildActivityRequest("key-act-B");

        // Act
        var result1 = await adapter.CreateOrUpdateAsync(request1);
        var result2 = await adapter.CreateOrUpdateAsync(request2);

        // Assert
        result1.ActivityId.Should().NotBe(result2.ActivityId, "import_keys diferentes → atividades diferentes");
    }

    // =========================================================================
    // OrganizationReadAdapter — BU
    // =========================================================================

    [Fact]
    public async Task GetBuIdByNameAsync_BuConfigurada_RetornaId()
    {
        // Arrange
        var adapter = new OrganizationReadAdapter();
        var buId = Guid.NewGuid();
        adapter.RegisterBu(_tenantId, "Comercial", buId);

        // Act
        var result = await adapter.GetBuIdByNameAsync("Comercial", _tenantId);

        // Assert
        result.Should().Be(buId, "BU pré-configurada deve ser retornada");
    }

    [Fact]
    public async Task GetBuIdByNameAsync_BuNaoConfigurada_RetornaNull()
    {
        // Arrange — DEP-04: BU ausente deve retornar null (MIG-ERR-010 é responsabilidade do caller).
        var adapter = new OrganizationReadAdapter();

        // Act
        var result = await adapter.GetBuIdByNameAsync("BU Inexistente", _tenantId);

        // Assert
        result.Should().BeNull("BU não configurada deve retornar null (MIG-ERR-010 no caller)");
    }

    [Fact]
    public async Task GetBuIdByNameAsync_BuDeOutroTenant_RetornaNull()
    {
        // Arrange — isolamento por tenant.
        var adapter = new OrganizationReadAdapter();
        var buId = Guid.NewGuid();
        var outroTenant = Guid.NewGuid();
        adapter.RegisterBu(outroTenant, "Comercial", buId);

        // Act — busca com tenant diferente do que registrou.
        var result = await adapter.GetBuIdByNameAsync("Comercial", _tenantId);

        // Assert
        result.Should().BeNull("BU de outro tenant não deve ser visível (isolamento)");
    }

    // =========================================================================
    // OrganizationReadAdapter — Estágio
    // =========================================================================

    [Fact]
    public async Task GetStageIdByNameAsync_EstagioConfigurado_RetornaId()
    {
        // Arrange
        var adapter = new OrganizationReadAdapter();
        var stageId = Guid.NewGuid();
        adapter.RegisterStage(_tenantId, "Proposta", stageId);

        // Act
        var result = await adapter.GetStageIdByNameAsync("Proposta", _tenantId);

        // Assert
        result.Should().Be(stageId);
    }

    [Fact]
    public async Task GetStageIdByNameAsync_EstagioAusente_RetornaNull()
    {
        // Arrange
        var adapter = new OrganizationReadAdapter();

        // Act
        var result = await adapter.GetStageIdByNameAsync("Lead Qualificado", _tenantId);

        // Assert
        result.Should().BeNull();
    }

    // =========================================================================
    // OrganizationReadAdapter — Usuário
    // =========================================================================

    [Fact]
    public async Task GetUserIdByNameAsync_UsuarioConfigurado_RetornaId()
    {
        // Arrange
        var adapter = new OrganizationReadAdapter();
        var userId = Guid.NewGuid();
        adapter.RegisterUser(_tenantId, "Milton", userId);

        // Act
        var result = await adapter.GetUserIdByNameAsync("Milton", _tenantId);

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserIdByNameAsync_UsuarioAusente_RetornaNull()
    {
        // Arrange — typo de nome deve ser tratado antes de chegar aqui (OwnerTypoMappingPolicy).
        var adapter = new OrganizationReadAdapter();

        // Act
        var result = await adapter.GetUserIdByNameAsync("Miilton", _tenantId);

        // Assert
        result.Should().BeNull("nome com typo não registrado deve retornar null");
    }

    [Fact]
    public async Task GetUserIdByNameAsync_NomeNormalizadoComTrim_RetornaId()
    {
        // Arrange — trim no nome cadastrado e buscado.
        var adapter = new OrganizationReadAdapter();
        var userId = Guid.NewGuid();
        adapter.RegisterUser(_tenantId, "  Milton  ", userId); // com espaços.

        // Act
        var result = await adapter.GetUserIdByNameAsync("Milton", _tenantId); // sem espaços.

        // Assert
        result.Should().Be(userId, "trim é aplicado em RegisterUser e na busca");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private ActivityImportRequest BuildActivityRequest(string importKey) =>
        new(
            OpportunityId: _opportunityId,
            Type: "Reunião",
            Description: "Reunião de apresentação de proposta",
            ActivityDate: new DateOnly(2026, 3, 15),
            OwnerId: _ownerId,
            ImportKey: importKey,
            TenantId: _tenantId);
}
