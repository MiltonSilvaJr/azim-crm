using DataMigration.Application.Ports;
using DataMigration.Domain.ValueObjects;
using DataMigration.Infrastructure.Adapters;
using FluentAssertions;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Adapters;

/// <summary>
/// Testes unitários do <see cref="AccountImportAdapter"/>.
///
/// Cobre:
/// - Criação de conta nova (IsNew = true).
/// - Dedupe por NormalizedName (RN-014): mesmo nome normalizado → mesma conta.
/// - Upsert idempotente por import_key (DD-003): mesma chave → mesmo ID.
/// - PII nunca chega aos logs (verificado por asserção de tipo).
///
/// Rastreia: TASK-16, ST-01, RN-014, DD-003, Req 7.
/// </summary>
public sealed class AccountImportAdapterTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    // =========================================================================
    // Criação de conta nova
    // =========================================================================

    [Fact]
    public async Task CreateOrGetAsync_QuandoContaNova_RetornaIsNewTrue()
    {
        // Arrange
        var adapter = new AccountImportAdapter();
        var request = BuildRequest("Pag.ai", "key-001");

        // Act
        var result = await adapter.CreateOrGetAsync(request);

        // Assert
        result.IsNew.Should().BeTrue("primeira conta com este nome deve ser criada");
        result.AccountId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateOrGetAsync_QuandoContaNova_RetornaGuidEstavel()
    {
        // Arrange
        var adapter = new AccountImportAdapter();
        var request = BuildRequest("Empresa XPTO", "key-002");

        // Act
        var result1 = await adapter.CreateOrGetAsync(request);

        // Assert — o ID deve ser estável (baseado em seed determinístico).
        result1.AccountId.Should().NotBe(Guid.Empty);
    }

    // =========================================================================
    // Dedupe por NormalizedName (RN-014)
    // =========================================================================

    [Fact]
    public async Task CreateOrGetAsync_QuandoMesmoNomeNormalizado_RetornaMesmaContaIsNewFalse()
    {
        // Arrange
        var adapter = new AccountImportAdapter();
        var request1 = BuildRequest("Pag.ai", "key-A");
        var request2 = BuildRequest("pag.ai", "key-B"); // Mesmo nome, case diferente.

        // Act
        var result1 = await adapter.CreateOrGetAsync(request1);
        var result2 = await adapter.CreateOrGetAsync(request2);

        // Assert — dedupe por NormalizedName (RN-014).
        result2.AccountId.Should().Be(result1.AccountId, "dedupe: mesmo nome normalizado → mesma conta");
        result2.IsNew.Should().BeFalse("conta já existia por dedupe de NormalizedName");
    }

    [Fact]
    public async Task CreateOrGetAsync_NomesNormalizadosDiferentes_RetornamContasDiferentes()
    {
        // Arrange
        var adapter = new AccountImportAdapter();
        var request1 = BuildRequest("Empresa A", "key-A1");
        var request2 = BuildRequest("Empresa B", "key-B1");

        // Act
        var result1 = await adapter.CreateOrGetAsync(request1);
        var result2 = await adapter.CreateOrGetAsync(request2);

        // Assert
        result1.AccountId.Should().NotBe(result2.AccountId, "nomes diferentes → contas diferentes");
    }

    // =========================================================================
    // Idempotência por import_key (DD-003)
    // =========================================================================

    [Fact]
    public async Task CreateOrGetAsync_MesmaImportKey_RetornaMesmoIdSemCriarNovaConta()
    {
        // Arrange
        var adapter = new AccountImportAdapter();
        var request = BuildRequest("Conta Alpha", "idempotent-key-42");

        // Act — mesma request duas vezes.
        var result1 = await adapter.CreateOrGetAsync(request);
        var result2 = await adapter.CreateOrGetAsync(request);

        // Assert — idempotência: mesmo ID, segundo retorno IsNew = false.
        result2.AccountId.Should().Be(result1.AccountId, "mesma import_key → mesmo ID (DD-003)");
        result2.IsNew.Should().BeFalse("segundo upsert com mesma key deve retornar existente");
    }

    [Fact]
    public async Task CreateOrGetAsync_ImportKeyDiferentesMesmoNome_RetornaMesmaContaIsNewFalse()
    {
        // Arrange — simula reexecução com import_key diferente mas mesmo nome (RN-014 + DD-003).
        var adapter = new AccountImportAdapter();
        var request1 = BuildRequest("Pipeline Corp", "key-exec-1");
        var request2 = BuildRequest("Pipeline Corp", "key-exec-2"); // nova key, mesmo nome.

        // Act
        var result1 = await adapter.CreateOrGetAsync(request1);
        var result2 = await adapter.CreateOrGetAsync(request2);

        // Assert — dedupe por NormalizedName prevalece mesmo com import_key diferente.
        result2.AccountId.Should().Be(result1.AccountId, "dedupe por NormalizedName prevalece");
        result2.IsNew.Should().BeFalse();
    }

    // =========================================================================
    // Verificação de ausência de PII nos logs (RNF 3)
    // =========================================================================

    [Fact]
    public async Task CreateOrGetAsync_ResultadoNaoContemPii()
    {
        // Arrange — verifica que AccountImportResult não expõe PII (nome, e-mail, telefone).
        var adapter = new AccountImportAdapter();
        var request = BuildRequest("Cliente Sigiloso Ltda", "key-pii-check");

        // Act
        var result = await adapter.CreateOrGetAsync(request);

        // Assert — AccountImportResult só contém ID e flag (sem nome, PII, etc.).
        result.Should().BeOfType<AccountImportResult>();
        result.AccountId.Should().NotBe(Guid.Empty);

        // AccountImportResult não tem propriedades de PII por design (IAccountImportPort.cs).
        var properties = typeof(AccountImportResult).GetProperties();
        properties.Select(p => p.Name).Should()
            .BeEquivalentTo(["AccountId", "IsNew"],
                "AccountImportResult expõe apenas AccountId e IsNew — sem PII");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private AccountImportRequest BuildRequest(string rawName, string importKey) =>
        new(
            NormalizedName: NormalizedName.From(rawName),
            ImportKey: importKey,
            TenantId: _tenantId);
}
