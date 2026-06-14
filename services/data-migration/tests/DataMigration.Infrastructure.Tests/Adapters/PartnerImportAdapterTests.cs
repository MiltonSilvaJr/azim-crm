using DataMigration.Application.Ports;
using DataMigration.Infrastructure.Adapters;
using FluentAssertions;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Adapters;

/// <summary>
/// Testes unitários do <see cref="PartnerImportAdapter"/>.
///
/// Cobre:
/// - Criação de parceiro novo (sem percentual).
/// - Dedupe por nome (case-insensitive).
/// - Idempotência por import_key (DD-003).
/// - Ausência de percentual (design §6.4, Req 3.4).
///
/// Rastreia: TASK-16, ST-01, DD-003, Req 5.2.
/// </summary>
public sealed class PartnerImportAdapterTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    // =========================================================================
    // Criação de parceiro novo (sem percentual)
    // =========================================================================

    [Fact]
    public async Task CreateOrGetAsync_QuandoParceirNovo_RetornaIsNewTrue()
    {
        // Arrange
        var adapter = new PartnerImportAdapter();
        var request = BuildRequest("Parceiro Alpha", "key-p-001");

        // Act
        var result = await adapter.CreateOrGetAsync(request);

        // Assert
        result.IsNew.Should().BeTrue("primeiro parceiro com este nome deve ser criado");
        result.PartnerId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateOrGetAsync_ParceiroSemPercentual_NaoArmazenaPercentual()
    {
        // Arrange — verifica que PartnerImportResult não expõe percentual (design §6.4).
        var adapter = new PartnerImportAdapter();
        var request = BuildRequest("Parceiro Sem Pct", "key-no-pct");

        // Act
        var result = await adapter.CreateOrGetAsync(request);

        // Assert — PartnerImportResult só tem PartnerId e IsNew.
        var properties = typeof(PartnerImportResult).GetProperties();
        properties.Select(p => p.Name).Should()
            .BeEquivalentTo(["PartnerId", "IsNew"],
                "sem percentual: PartnerImportResult expõe apenas PartnerId e IsNew");
    }

    // =========================================================================
    // Dedupe por nome
    // =========================================================================

    [Fact]
    public async Task CreateOrGetAsync_MesmoNomeCaseDiferente_RetornaMesmoParceiro()
    {
        // Arrange
        var adapter = new PartnerImportAdapter();
        var request1 = BuildRequest("Venda Direta", "key-v1");
        var request2 = BuildRequest("venda direta", "key-v2");

        // Act
        var result1 = await adapter.CreateOrGetAsync(request1);
        var result2 = await adapter.CreateOrGetAsync(request2);

        // Assert
        result2.PartnerId.Should().Be(result1.PartnerId, "dedupe case-insensitive");
        result2.IsNew.Should().BeFalse();
    }

    // =========================================================================
    // Idempotência por import_key (DD-003)
    // =========================================================================

    [Fact]
    public async Task CreateOrGetAsync_MesmaImportKey_RetornaMesmoId()
    {
        // Arrange
        var adapter = new PartnerImportAdapter();
        var request = BuildRequest("Parceiro Beta", "key-idempotent-p");

        // Act
        var result1 = await adapter.CreateOrGetAsync(request);
        var result2 = await adapter.CreateOrGetAsync(request);

        // Assert
        result2.PartnerId.Should().Be(result1.PartnerId, "mesma import_key → mesmo ID (DD-003)");
        result2.IsNew.Should().BeFalse();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private PartnerImportRequest BuildRequest(string name, string importKey) =>
        new(Name: name, ImportKey: importKey, TenantId: _tenantId);
}
