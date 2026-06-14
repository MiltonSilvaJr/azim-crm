using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.ValueObjects;
using DataMigration.Infrastructure.Adapters;
using FluentAssertions;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Adapters;

/// <summary>
/// Testes unitários de <see cref="OpportunityNumberAdapter"/> e <see cref="OpportunityImportAdapter"/>.
///
/// Cobre:
/// - Alocação de número AZ-NNNN com sequência ≥ 95 (Req 8).
/// - Colisão entre número preservado e gerado impossível (ADR-0003).
/// - Owner ausente lança MIG-ERR-006 (RN-002).
/// - Upsert idempotente por import_key (DD-003).
/// - Preservação de número em reexecução (Req 12.3).
///
/// Rastreia: TASK-17, ST-01, ADR-0003, RN-001, RN-002, DD-003, Req 8.
/// </summary>
public sealed class OpportunityAdapterTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _stageId = Guid.NewGuid();

    // =========================================================================
    // OpportunityNumberAdapter — alocação de sequência
    // =========================================================================

    [Fact]
    public async Task AllocateNextAsync_PrimeiraAlocacao_RetornaAz0095()
    {
        // Arrange — sequência inicial ≥ 95 (Req 8, tenant Vellus).
        var adapter = new OpportunityNumberAdapter();

        // Act
        var number = await adapter.AllocateNextAsync(_tenantId);

        // Assert
        number.SequenceNumber.Should().Be(95, "sequência começa em 95 (Req 8)");
        number.Value.Should().Be("AZ-0095");
    }

    [Fact]
    public async Task AllocateNextAsync_AlococoesSequenciais_IncrementaCorretamente()
    {
        // Arrange
        var adapter = new OpportunityNumberAdapter();

        // Act
        var n1 = await adapter.AllocateNextAsync(_tenantId);
        var n2 = await adapter.AllocateNextAsync(_tenantId);
        var n3 = await adapter.AllocateNextAsync(_tenantId);

        // Assert
        n1.SequenceNumber.Should().Be(95);
        n2.SequenceNumber.Should().Be(96);
        n3.SequenceNumber.Should().Be(97);
    }

    [Fact]
    public async Task AllocateNextAsync_TenantsDiferentes_SequenciasIndependentes()
    {
        // Arrange
        var adapter = new OpportunityNumberAdapter();
        var tenantB = Guid.NewGuid();

        // Act
        var nA = await adapter.AllocateNextAsync(_tenantId);
        var nB = await adapter.AllocateNextAsync(tenantB);

        // Assert — cada tenant tem sequência independente (ADR-0003).
        // Ambos começam em 95 pois as sequências são por tenant.
        nA.SequenceNumber.Should().Be(95);
        nB.SequenceNumber.Should().Be(95, "tenant B também começa em 95, sequência independente");
        nA.Value.Should().Be(nB.Value, "ambos são AZ-0095 — o número é igual por design (sequências separadas por tenant)");
    }

    [Fact]
    public async Task ReserveNumber_NumeroPreservadoNaSequencia_AvancaAlem()
    {
        // Arrange — número preservado AZ-0100 deve ser excluído da geração futura.
        var adapter = new OpportunityNumberAdapter();
        var preservedNumber = OpportunityNumber.FromSequence(100);

        // Act
        adapter.ReserveNumber(_tenantId, preservedNumber);
        var next = await adapter.AllocateNextAsync(_tenantId);

        // Assert — próximo número gerado deve ser > 100 (sem colisão).
        next.SequenceNumber.Should().BeGreaterThan(100,
            "número preservado 100 não pode colidir com número gerado (ADR-0003)");
    }

    [Fact]
    public async Task GetCurrentSequenceAsync_SemAlocacao_RetornaZero()
    {
        // Arrange
        var adapter = new OpportunityNumberAdapter();

        // Act
        var seq = await adapter.GetCurrentSequenceAsync(_tenantId);

        // Assert
        seq.Should().Be(0, "sem alocação, sequência atual é 0");
    }

    // =========================================================================
    // OpportunityImportAdapter — criação e upsert
    // =========================================================================

    [Fact]
    public async Task CreateOrUpdateAsync_OportunidadeNova_RetornaIsNewTrue()
    {
        // Arrange
        var adapter = new OpportunityImportAdapter();
        var request = BuildRequest(OpportunityNumber.FromSequence(95), "key-opp-001");

        // Act
        var result = await adapter.CreateOrUpdateAsync(request);

        // Assert
        result.IsNew.Should().BeTrue();
        result.OpportunityId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_OwnerAusente_LancaMigErr006()
    {
        // Arrange — owner obrigatório (RN-002).
        var adapter = new OpportunityImportAdapter();
        var request = BuildRequest(
            OpportunityNumber.FromSequence(95),
            "key-no-owner",
            ownerId: Guid.Empty); // owner vazio → deve rejeitar.

        // Act
        var act = async () => await adapter.CreateOrUpdateAsync(request);

        // Assert
        await act.Should().ThrowAsync<MigrationDomainException>()
            .Where(e => e.ErrorCode == "MIG-ERR-006",
                "owner obrigatório ausente deve lançar MIG-ERR-006 (RN-002)");
    }

    [Fact]
    public async Task CreateOrUpdateAsync_MesmaImportKey_RetornaMesmoId()
    {
        // Arrange — idempotência por import_key (DD-003).
        var adapter = new OpportunityImportAdapter();
        var request = BuildRequest(OpportunityNumber.FromSequence(95), "key-idem-opp");

        // Act
        var result1 = await adapter.CreateOrUpdateAsync(request);
        var result2 = await adapter.CreateOrUpdateAsync(request);

        // Assert
        result2.OpportunityId.Should().Be(result1.OpportunityId, "mesma import_key → mesmo ID");
        result2.IsNew.Should().BeFalse();
    }

    [Fact]
    public async Task CreateOrUpdateAsync_NumeroPreservadoReutilizado_RetornaMesmaOportunidade()
    {
        // Arrange — simula reexecução: mesmo AZ-NNNN com import_key diferente (Req 12.3).
        var adapter = new OpportunityImportAdapter();
        var number = OpportunityNumber.FromSequence(100);
        var request1 = BuildRequest(number, "key-preserved-1");
        var request2 = BuildRequest(number, "key-preserved-2"); // nova key, mesmo número.

        // Act
        var result1 = await adapter.CreateOrUpdateAsync(request1);
        var result2 = await adapter.CreateOrUpdateAsync(request2);

        // Assert — número preservado mantido; mesma oportunidade (Req 12.3).
        result2.OpportunityId.Should().Be(result1.OpportunityId,
            "número preservado é chave de dedupe — sem duplicação em reexecução");
    }

    [Fact]
    public async Task ResolveIdByNumberAsync_NumeroExistente_RetornaId()
    {
        // Arrange
        var adapter = new OpportunityImportAdapter();
        var number = OpportunityNumber.FromSequence(95);
        var request = BuildRequest(number, "key-resolve-001");
        await adapter.CreateOrUpdateAsync(request);

        // Act
        var resolvedId = await adapter.ResolveIdByNumberAsync(number, _tenantId);

        // Assert
        resolvedId.Should().NotBeNull("oportunidade com este número deve existir");
        resolvedId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ResolveIdByNumberAsync_NumeroNaoEncontrado_RetornaNull()
    {
        // Arrange
        var adapter = new OpportunityImportAdapter();
        var number = OpportunityNumber.FromSequence(9999);

        // Act
        var resolvedId = await adapter.ResolveIdByNumberAsync(number, _tenantId);

        // Assert — número não encontrado deve retornar null (Req 10.4).
        resolvedId.Should().BeNull("número não encontrado deve retornar null");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private OpportunityImportRequest BuildRequest(
        OpportunityNumber number,
        string importKey,
        Guid? ownerId = null) =>
        new(
            OpportunityNumber: number,
            Title: $"Oportunidade {number.Value}",
            AccountId: _accountId,
            OwnerId: ownerId ?? _ownerId,
            StageId: _stageId,
            PartnerId: null,
            ValorSetupCents: 500_00L,     // R$ 500,00 em centavos.
            ValorMensalCents: 1_000_00L,  // R$ 1.000,00 em centavos.
            Meses: 12,
            ForecastPonderadoCents: 600_00L,
            CloseDate: new DateOnly(2026, 12, 31),
            ImportKey: importKey,
            TenantId: _tenantId);
}
