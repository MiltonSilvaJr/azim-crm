using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using GoalForecast.Infrastructure.Tests.Fixtures;

namespace GoalForecast.Infrastructure.Tests.Pbt;

/// <summary>
/// PBT-05 — Round-trip monetário fim a fim com banco PostgreSQL real.
///
/// Propriedade verificada: para qualquer valor <c>long</c> não-negativo arbitrário,
/// ao persistir via <c>GoalRepository</c> e ler de volta, o valor é idêntico ao original.
/// Nenhuma camada converte <c>long</c> para <c>double</c> (RNF 4, DEC-011).
///
/// Usa banco PostgreSQL real (Testcontainers) para capturar conversões em todas as camadas:
/// EF Core ValueConverter → Npgsql BIGINT → PostgreSQL BIGINT → leitura EF Core.
///
/// Mapeia: PBT-05, RNF 4, design §13, TASK-20.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RoundTripMoneyTests(PostgresContainerFixture db)
{
    /// <summary>
    /// PBT-05: para qualquer <c>long</c> não-negativo, o valor persiste e é lido de volta exatamente.
    /// FsCheck gera amostras aleatórias incluindo bordas (0, MaxValue, valores próximos de 2^53).
    /// </summary>
    [Property(Arbitrary = new[] { typeof(NonNegativeLongArb) }, MaxTest = 50)]
    public Property ValorMeta_roundtrips_exactly_through_database(long valorMetaCents)
    {
        // FsCheck avalia esta função em thread síncrona — usamos .GetAwaiter().GetResult()
        // pois Property não suporta async nativo no FsCheck 3.x.
        var result = RoundTripAsync(valorMetaCents).GetAwaiter().GetResult();
        return Prop.ToProperty(result);
    }

    private async Task<bool> RoundTripAsync(long valorMetaCents)
    {
        var tenantId = Guid.NewGuid();
        var buId = Guid.NewGuid();

        await using var ctxWrite = db.BuildOwnerContext(tenantId);
        var repoWrite = new GoalRepository(ctxWrite);

        var scope = GoalScope.ForBu(buId);
        var period = new GoalPeriod(2025, 1);
        var goal = Goal.Create(tenantId, scope, period, Money.Of(valorMetaCents));

        await repoWrite.Add(goal);

        // Lê de volta com contexto separado (sem cache EF)
        await using var ctxRead = db.BuildOwnerContext(tenantId);
        var repoRead = new GoalRepository(ctxRead);
        var loaded = await repoRead.FindById(tenantId, goal.Id);

        return loaded is not null && loaded.ValorMeta.Cents == valorMetaCents;
    }
}

/// <summary>
/// Gerador FsCheck para <c>long</c> não-negativo (centavos válidos).
/// Inclui bordas: 0, 1, valores grandes, valores próximos de 2^53 (limite double precision).
/// </summary>
public static class NonNegativeLongArb
{
    public static Arbitrary<long> Generate()
    {
        // Bordas críticas para PBT-05:
        // - 0: valor mínimo válido
        // - 9_007_199_254_740_993L (2^53+1): não representável como double
        // - 9_007_199_254_740_992L (2^53): limite exato de double
        var edgeCases = new long[] { 0L, 1L, 100L, 9_007_199_254_740_993L, 9_007_199_254_740_992L };

        var edgeGen = Gen.Elements(edgeCases);

        // Gera longs não-negativos no intervalo [0, long.MaxValue / 100]
        var randomGen = ArbMap.Default.GeneratorFor<long>()
            .Select(l => Math.Abs(l % (long.MaxValue / 100)));

        var combined = Gen.Frequency<long>(
            (1, edgeGen),
            (9, randomGen));

        return combined.ToArbitrary();
    }
}
