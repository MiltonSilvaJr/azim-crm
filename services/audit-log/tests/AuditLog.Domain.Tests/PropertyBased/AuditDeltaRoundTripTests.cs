using AuditLog.Domain.ValueObjects;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AuditLog.Domain.Tests.PropertyBased;

/// <summary>
/// PBT-03 — Round-trip do delta.
/// Verifica que as três variantes de <see cref="AuditDelta"/> preservam os dados
/// sem perda: create reconstrói estado inicial, update reproduz estado posterior,
/// delete reconstrói último estado. Inclui campos monetários em centavos (<see langword="long"/>).
/// </summary>
public sealed class AuditDeltaRoundTripTests
{
    // ------------------------------------------------------------------ Tipos auxiliares

    /// <summary>Estado de uma entidade como mapa de campo → valor.</summary>
    public sealed record EntityState(IReadOnlyDictionary<string, object?> Fields);

    /// <summary>Par de estados (before, after) para testes de update.</summary>
    public sealed record StatePair(
        IReadOnlyDictionary<string, object?> Before,
        IReadOnlyDictionary<string, object?> After);

    // ------------------------------------------------------------------ Geradores FsCheck

    /// <summary>
    /// Arbitrários para gerar estados de entidade com campos string, long (monetários) e bool.
    /// </summary>
    private static class StateArbitraries
    {
        /// <summary>
        /// Gera um campo simples com valor string, long ou bool (nunca float/double/decimal).
        /// Long representa campos monetários em centavos.
        /// </summary>
        private static Gen<(string Key, object? Value)> FieldGen()
        {
            var stringField =
                from k in Arb.Generate<NonEmptyString>()
                from v in Arb.Generate<string>()
                select (k.Get.Replace("\0", ""), (object?)v);

            var longField =
                from k in Arb.Generate<NonEmptyString>()
                from v in Arb.Generate<long>()
                select (k.Get.Replace("\0", "") + "_cents", (object?)v);

            var boolField =
                from k in Arb.Generate<NonEmptyString>()
                from v in Arb.Generate<bool>()
                select (k.Get.Replace("\0", "") + "_flag", (object?)v);

            return Gen.OneOf(stringField, longField, boolField);
        }

        /// <summary>
        /// Gera um <see cref="EntityState"/> com entre 1 e 5 campos únicos.
        /// </summary>
        public static Arbitrary<EntityState> EntityStateArb()
        {
            var gen =
                from count in Gen.Choose(1, 5)
                from fields in Gen.ListOf(count, FieldGen())
                // Garante chaves únicas e não-vazias (nullable safe)
                let unique = fields
                    .Where(f => !string.IsNullOrEmpty(f.Key))
                    .GroupBy(f => f.Key)
                    .Select(g => g.First())
                    .ToDictionary(f => f.Key, f => f.Value)
                where unique.Count >= 1
                select new EntityState(unique.AsReadOnly());

            return Arb.From(gen);
        }

        /// <summary>
        /// Gera um <see cref="StatePair"/> onde pelo menos um campo foi alterado.
        /// </summary>
        public static Arbitrary<StatePair> StatePairArb()
        {
            var gen =
                from before in EntityStateArb().Generator
                from changeCount in Gen.Choose(1, Math.Max(1, before.Fields.Count))
                from suffix in Arb.Generate<NonEmptyString>()
                let changedKeys = before.Fields.Keys.Take(changeCount).ToList()
                let afterFields = BuildAfterFields(before.Fields, changedKeys, suffix.Get)
                let changesOnly = afterFields
                    .Where(kvp => !Equals(kvp.Value, before.Fields[kvp.Key]))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                where changesOnly.Count >= 1
                select new StatePair(
                    before.Fields,
                    changesOnly.AsReadOnly());

            return Arb.From(gen);
        }

        private static Dictionary<string, object?> BuildAfterFields(
            IReadOnlyDictionary<string, object?> before,
            IEnumerable<string> changedKeys,
            string suffix)
        {
            var after = before.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            foreach (var key in changedKeys)
            {
                // Adiciona sufixo para garantir valor diferente do original
                after[key] = after[key]?.ToString() + "_" + suffix.Replace("\0", "x") + "_changed";
            }
            return after;
        }
    }

    // ------------------------------------------------------------------ PBT-03 ForCreate

    /// <summary>
    /// PBT-03 (create): o delta de criação reconstrói exatamente o estado inicial.
    /// Para qualquer estado arbitrário, AuditDelta.After deve ser igual ao estado fornecido.
    /// Mínimo 100 amostras.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = new[] { typeof(StateArbitraries) })]
    public bool PBT03_ForCreate_ReconstroisEstadoInicial(EntityState state)
    {
        var delta = AuditDelta.ForCreate(state.Fields);

        // Round-trip: After reproduz fieldo a campo o estado inicial
        if (delta.After is null) return false;
        if (delta.After.Count != state.Fields.Count) return false;

        foreach (var (key, value) in state.Fields)
        {
            if (!delta.After.TryGetValue(key, out var deltaValue)) return false;
            if (!Equals(deltaValue, value)) return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ PBT-03 ForDelete

    /// <summary>
    /// PBT-03 (delete): o delta de exclusão reconstrói exatamente o último estado conhecido.
    /// Para qualquer estado arbitrário, AuditDelta.Before deve ser igual ao estado fornecido.
    /// Mínimo 100 amostras.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = new[] { typeof(StateArbitraries) })]
    public bool PBT03_ForDelete_ReconstroisUltimoEstado(EntityState state)
    {
        var delta = AuditDelta.ForDelete(state.Fields);

        if (delta.Before is null) return false;
        if (delta.Before.Count != state.Fields.Count) return false;

        foreach (var (key, value) in state.Fields)
        {
            if (!delta.Before.TryGetValue(key, out var deltaValue)) return false;
            if (!Equals(deltaValue, value)) return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ PBT-03 ForUpdate

    /// <summary>
    /// PBT-03 (update): aplicar os valores "after" dos Changes sobre o estado "before"
    /// reproduz o estado posterior para todos os campos alterados.
    /// Mínimo 100 amostras.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = new[] { typeof(StateArbitraries) })]
    public bool PBT03_ForUpdate_AplicarChangesReproduzEstadoPosterior(StatePair pair)
    {
        // Constrói o delta de update com os campos que realmente mudaram
        var changes = pair.After.ToDictionary(
            kvp => kvp.Key,
            kvp => new AuditAttributeChange(pair.Before[kvp.Key], kvp.Value));

        var delta = AuditDelta.ForUpdate(changes.AsReadOnly());

        // Round-trip: aplicar delta.Changes["x"].After sobre o estado Before => reproduz After
        foreach (var (key, change) in delta.Changes!)
        {
            var expectedAfterValue = pair.After[key];
            if (!Equals(change.After, expectedAfterValue)) return false;

            var expectedBeforeValue = pair.Before[key];
            if (!Equals(change.Before, expectedBeforeValue)) return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ PBT-03 campos monetários

    /// <summary>
    /// PBT-03 (monetário): deltas com campos long (centavos) não perdem precisão.
    /// Garante que inteiros em centavos sobrevivem ao round-trip sem conversão para ponto flutuante.
    /// Mínimo 100 amostras.
    /// </summary>
    [Property(MaxTest = 200)]
    public bool PBT03_CamposMonetarios_LongNaoPerdemPrecisao(long amountCents)
    {
        var state = new Dictionary<string, object?>
        {
            ["amount_cents"] = amountCents,
            ["name"] = "TestEntity"
        }.AsReadOnly();

        var delta = AuditDelta.ForCreate(state);

        return delta.After!.TryGetValue("amount_cents", out var retrieved)
            && retrieved is long retrievedLong
            && retrievedLong == amountCents;
    }

    /// <summary>
    /// PBT-03 (monetário update): valores long em Changes sobrevivem ao round-trip.
    /// </summary>
    [Property(MaxTest = 200)]
    public bool PBT03_CamposMonetariosEmUpdate_LongPreservados(long before, long after)
    {
        // Garante before != after para satisfazer o guard
        if (before == after) return true; // amostra trivial, descarta

        var changes = new Dictionary<string, AuditAttributeChange>
        {
            ["amount_cents"] = new AuditAttributeChange(before, after)
        }.AsReadOnly();

        var delta = AuditDelta.ForUpdate(changes);

        return delta.Changes!.TryGetValue("amount_cents", out var change)
            && change.Before is long b && b == before
            && change.After is long a && a == after;
    }

    // ------------------------------------------------------------------ Serialização idempotente

    /// <summary>
    /// PBT-03 (serialização): igualdade estrutural do AuditDelta é idempotente —
    /// comparar o mesmo delta consigo mesmo retorna sempre verdadeiro.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = new[] { typeof(StateArbitraries) })]
    public bool PBT03_IgualdadeEstruturalIdempotente(EntityState state)
    {
        var delta1 = AuditDelta.ForCreate(state.Fields);
        var delta2 = AuditDelta.ForCreate(state.Fields);

        // Dois deltas com o mesmo conteúdo devem ser estruturalmente iguais
        return delta1.Equals(delta2);
    }
}
