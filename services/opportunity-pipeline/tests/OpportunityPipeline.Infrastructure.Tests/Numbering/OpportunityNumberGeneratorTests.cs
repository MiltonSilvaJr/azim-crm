using Microsoft.EntityFrameworkCore;
using OpportunityPipeline.Application.Common;
using OpportunityPipeline.Infrastructure.Numbering;
using OpportunityPipeline.Infrastructure.Persistence;

namespace OpportunityPipeline.Infrastructure.Tests.Numbering;

/// <summary>
/// Testes do OpportunityNumberGenerator — numeração atômica por tenant.
/// Inclui teste de concorrência (PBT-01): sem duplicatas sob carga concorrente.
/// Mapeia: TASK-15, DD-001, Req 3, PBT-01.
/// </summary>
[Collection("PostgresCollection")]
public sealed class OpportunityNumberGeneratorTests(PostgresFixture fixture)
{
    // =========================================================================
    // Teste 1: Gera número no formato AZ-NNNN
    // =========================================================================

    [Fact(DisplayName = "NUM_01: NextAsync deve retornar número no formato AZ-NNNN")]
    public async Task NextAsync_ShouldReturnFormattedNumber()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = await fixture.CreateContextAsync(tenantId);
        var generator = new OpportunityNumberGenerator(ctx);

        // Act
        var number = await generator.NextAsync(tenantId);

        // Assert
        number.Value.Should().MatchRegex(@"^AZ-\d{4,}$",
            "o número deve estar no formato AZ-NNNN (Req 3, DD-001).");
    }

    // =========================================================================
    // Teste 2: Números sequenciais são estritamente crescentes
    // =========================================================================

    [Fact(DisplayName = "NUM_02: números gerados sequencialmente são estritamente crescentes")]
    public async Task NextAsync_Sequential_ShouldBeStrictlyIncreasing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = await fixture.CreateContextAsync(tenantId);
        var generator = new OpportunityNumberGenerator(ctx);

        // Act
        var n1 = await generator.NextAsync(tenantId);
        var n2 = await generator.NextAsync(tenantId);
        var n3 = await generator.NextAsync(tenantId);

        // Assert: extrair sequência numérica
        var seq1 = ExtractSequence(n1.Value);
        var seq2 = ExtractSequence(n2.Value);
        var seq3 = ExtractSequence(n3.Value);

        seq2.Should().BeGreaterThan(seq1, "números sequenciais devem ser crescentes.");
        seq3.Should().BeGreaterThan(seq2, "números sequenciais devem ser crescentes.");
    }

    // =========================================================================
    // Teste 3: Tenants independentes — sem interferência
    // =========================================================================

    [Fact(DisplayName = "NUM_03: tenants diferentes têm contadores independentes (sem interferência)")]
    public async Task NextAsync_DifferentTenants_HaveIndependentCounters()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var ctxA = await fixture.CreateContextAsync(tenantA);
        await using var ctxB = await fixture.CreateContextAsync(tenantB);

        var generatorA = new OpportunityNumberGenerator(ctxA);
        var generatorB = new OpportunityNumberGenerator(ctxB);

        // Act: gera 3 para tenant A, depois inicia tenant B do 1
        await generatorA.NextAsync(tenantA);
        await generatorA.NextAsync(tenantA);
        await generatorA.NextAsync(tenantA);

        var nB1 = await generatorB.NextAsync(tenantB);

        // Assert: tenant B começa do 1 (independente do tenant A)
        ExtractSequence(nB1.Value).Should().Be(1,
            "o contador do tenant B deve ser independente do tenant A (DD-001).");
    }

    // =========================================================================
    // PBT-01: Teste de concorrência — sem duplicatas sob carga concorrente
    // =========================================================================

    [Fact(DisplayName = "PBT-01: geração concorrente de números não deve produzir duplicatas")]
    public async Task NextAsync_ConcurrentGeneration_ShouldProduceNoDuplicates()
    {
        // Arrange: 20 gerações concorrentes para o mesmo tenant
        const int concurrency = 20;
        var tenantId = Guid.NewGuid();

        // Act: gera concorrentemente — cada uma com seu próprio DbContext
        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            await using var ctx = await fixture.CreateContextAsync(tenantId);
            var generator = new OpportunityNumberGenerator(ctx);
            return await generator.NextAsync(tenantId);
        });

        var numbers = await Task.WhenAll(tasks);

        // Assert: todos os números devem ser únicos
        var distinct = numbers.Select(n => n.Value).Distinct().Count();
        distinct.Should().Be(concurrency,
            $"todos os {concurrency} números gerados concorrentemente devem ser únicos (PBT-01, DD-001).");

        // Todos devem ter formato válido
        foreach (var n in numbers)
        {
            n.Value.Should().MatchRegex(@"^AZ-\d{4,}$",
                "todos os números devem estar no formato AZ-NNNN.");
        }
    }

    // =========================================================================
    // Teste auxiliar
    // =========================================================================

    private static long ExtractSequence(string numberValue)
    {
        // AZ-NNNN → extrai NNNN como long
        var parts = numberValue.Split('-');
        return long.Parse(parts[1]);
    }
}
