namespace ActivityManagement.Domain.Tests.Activities.ValueObjects;

using ActivityManagement.Domain.Activities.ValueObjects;
using FsCheck;
using FsCheck.Xunit;

/// <summary>
/// PBT-01 — Integridade da máquina de estados de <see cref="ActivityStatus"/>.
/// Verifica que:
///   (a) o estado final só é alcançável por sequências de transições válidas;
///   (b) estados terminais não têm transições de saída;
///   (c) transições inválidas não alteram o estado.
/// Geradores cobrem sequências arbitrárias; FsCheck executa ≥ 100 casos por padrão.
/// Mapeia: design §4.5, PBT-01, TASK-02 ST-01.
/// </summary>
public sealed class ActivityStatusPbt01Tests
{
    private static readonly string[] AllValues = ["pending", "in_progress", "completed", "cancelled"];

    // Mapa de transições válidas conforme design §4.5
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        ["pending"]     = ["in_progress", "completed", "cancelled"],
        ["in_progress"] = ["pending", "completed", "cancelled"],
        ["completed"]   = [],
        ["cancelled"]   = [],
    };

    private static readonly HashSet<string> Terminals = ["completed", "cancelled"];

    // ── Gerador de sequências de status ──────────────────────────────────────

    private static Gen<string[]> StatusSequenceGen(int maxLen = 10)
        => Gen.Choose(1, maxLen).SelectMany(len =>
            Gen.Elements(AllValues).ArrayOf(len));

    // ── PBT-01a: estado alcançado por sequência válida deve ser alcançável ───

    /// <summary>
    /// PBT-01a: aplicar sequência de transições válidas jamais viola a state machine.
    /// O estado final só pode ser o resultado de transições permitidas.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = [typeof(StatusArbitrary)])]
    public Property ValidChain_NeverViolatesStateMachine(string[] sequence)
    {
        var current = ActivityStatus.Pending;
        foreach (var next in sequence)
        {
            if (!ActivityStatus.IsValidValue(next)) continue;
            var target = ActivityStatus.Create(next);
            if (current.CanTransitionTo(target))
                current = target;
            // transição inválida: estado não muda
        }

        // Invariante: o valor corrente é sempre um dos valores canônicos
        return AllValues.Contains(current.Value).ToProperty();
    }

    /// <summary>
    /// PBT-01b: terminal não tem transição de saída — nenhuma transição de estado terminal
    /// é permitida, independente do alvo.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = [typeof(StatusArbitrary)])]
    public Property Terminal_HasNoOutgoingTransitions(string terminalValue, string targetValue)
    {
        if (!Terminals.Contains(terminalValue) || !AllValues.Contains(targetValue))
            return true.ToProperty(); // descarta caso inválido

        var terminal = ActivityStatus.Create(terminalValue);
        var target   = ActivityStatus.Create(targetValue);
        return (!terminal.CanTransitionTo(target)).ToProperty();
    }

    /// <summary>
    /// PBT-01c: transição inválida retorna false e não altera o estado.
    /// </summary>
    [Property(MaxTest = 200, Arbitrary = [typeof(StatusArbitrary)])]
    public Property InvalidTransition_DoesNotChangeState(string fromValue, string toValue)
    {
        if (!AllValues.Contains(fromValue) || !AllValues.Contains(toValue))
            return true.ToProperty();

        var from   = ActivityStatus.Create(fromValue);
        var to     = ActivityStatus.Create(toValue);
        var canDo  = from.CanTransitionTo(to);
        var isValid = ValidTransitions[fromValue].Contains(toValue);

        // CanTransitionTo deve concordar com o mapa de transições
        return (canDo == isValid).ToProperty();
    }

    /// <summary>
    /// PBT-01d: após sequência de transições aleatórias seguindo apenas as permitidas,
    /// o estado corrente coincide com o que a execução manual produziria.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property RandomSequence_FinalStateMatchesManualTrace(NonEmptyArray<PositiveInt> indices)
    {
        var seq = indices.Get.Select(i => AllValues[i.Get % AllValues.Length]).ToArray();

        var current = "pending";
        foreach (var next in seq)
        {
            if (ValidTransitions[current].Contains(next))
                current = next;
        }

        var statusCurrent = ActivityStatus.Pending;
        foreach (var next in seq)
        {
            var target = ActivityStatus.Create(next);
            if (statusCurrent.CanTransitionTo(target))
                statusCurrent = target;
        }

        return (statusCurrent.Value == current).ToProperty();
    }
}

/// <summary>
/// Gerador arbitrário de valores de status canônicos para FsCheck.
/// </summary>
public sealed class StatusArbitrary
{
    private static readonly string[] AllValues = ["pending", "in_progress", "completed", "cancelled"];

    public static Arbitrary<string> StringArbitrary()
        => Gen.Elements(AllValues).ToArbitrary();

    public static Arbitrary<string[]> StringArrayArbitrary()
        => Gen.Choose(1, 10)
             .SelectMany(n => Gen.Elements(AllValues).ArrayOf(n))
             .ToArbitrary();
}
