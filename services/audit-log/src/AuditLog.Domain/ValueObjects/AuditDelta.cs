namespace AuditLog.Domain.ValueObjects;

/// <summary>
/// Diferencial estruturado do estado de uma entidade de negócio em uma operação auditada.
/// Imutável após criação; representa uma das três variantes: <c>create</c>, <c>update</c> ou <c>delete</c>.
/// Valores monetários devem ser representados em centavos inteiros (<see cref="long"/>);
/// tipos <see cref="float"/>, <see cref="double"/> e <see cref="decimal"/> são proibidos (DD-005).
/// </summary>
public sealed record AuditDelta
{
    /// <summary>Discriminante que indica qual variante do delta está presente.</summary>
    public AuditDeltaKind Kind { get; private init; }

    /// <summary>
    /// Estado completo após criação.
    /// Preenchido apenas quando <see cref="Kind"/> é <see cref="AuditDeltaKind.Create"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? After { get; private init; }

    /// <summary>
    /// Estado completo antes da exclusão.
    /// Preenchido apenas quando <see cref="Kind"/> é <see cref="AuditDeltaKind.Delete"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Before { get; private init; }

    /// <summary>
    /// Atributos efetivamente alterados, cada um com par before/after.
    /// Preenchido apenas quando <see cref="Kind"/> é <see cref="AuditDeltaKind.Update"/>.
    /// </summary>
    public IReadOnlyDictionary<string, AuditAttributeChange>? Changes { get; private init; }

    private AuditDelta() { }

    // ------------------------------------------------------------------ Factory methods

    /// <summary>
    /// Cria um delta de criação com o estado inicial completo da entidade.
    /// Não inclui estado anterior (REQ-003.2).
    /// </summary>
    /// <param name="after">Estado da entidade após a criação. Não pode ser nulo nem vazio.</param>
    /// <exception cref="ArgumentNullException">Se <paramref name="after"/> for nulo.</exception>
    /// <exception cref="ArgumentException">
    /// Se <paramref name="after"/> for vazio ou contiver valores do tipo
    /// <see cref="float"/>, <see cref="double"/> ou <see cref="decimal"/>.
    /// </exception>
    public static AuditDelta ForCreate(IReadOnlyDictionary<string, object?> after)
    {
        ArgumentNullException.ThrowIfNull(after);

        if (after.Count == 0)
            throw new ArgumentException(
                "O delta de criação não pode ser vazio.",
                nameof(after));

        ValidateNoForbiddenNumericTypes(after.Values);

        return new AuditDelta { Kind = AuditDeltaKind.Create, After = after };
    }

    /// <summary>
    /// Cria um delta de atualização contendo apenas os atributos efetivamente alterados (REQ-003.4).
    /// Cada entrada deve conter um par (before, after) onde before ≠ after.
    /// </summary>
    /// <param name="changes">
    /// Atributos alterados com seus valores antes e depois.
    /// Não pode ser nulo nem vazio; cada atributo deve ter before ≠ after.
    /// </param>
    /// <exception cref="ArgumentNullException">Se <paramref name="changes"/> for nulo.</exception>
    /// <exception cref="ArgumentException">
    /// Se <paramref name="changes"/> for vazio, contiver atributo sem mudança efetiva (before == after),
    /// ou contiver valores do tipo <see cref="float"/>, <see cref="double"/> ou <see cref="decimal"/>.
    /// </exception>
    public static AuditDelta ForUpdate(IReadOnlyDictionary<string, AuditAttributeChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
            throw new ArgumentException(
                "O delta de atualização deve conter pelo menos um atributo efetivamente alterado.",
                nameof(changes));

        foreach (var (key, change) in changes)
        {
            if (Equals(change.Before, change.After))
                throw new ArgumentException(
                    $"O atributo '{key}' não foi alterado (before == after). " +
                    "ForUpdate aceita apenas atributos com mudança efetiva (REQ-003.4).",
                    nameof(changes));
        }

        var allValues = changes.Values.SelectMany(c => new[] { c.Before, c.After });
        ValidateNoForbiddenNumericTypes(allValues);

        return new AuditDelta { Kind = AuditDeltaKind.Update, Changes = changes };
    }

    /// <summary>
    /// Cria um delta de exclusão com o último estado conhecido da entidade.
    /// Não inclui estado posterior (REQ-003.3).
    /// </summary>
    /// <param name="before">Estado da entidade antes da exclusão. Não pode ser nulo nem vazio.</param>
    /// <exception cref="ArgumentNullException">Se <paramref name="before"/> for nulo.</exception>
    /// <exception cref="ArgumentException">
    /// Se <paramref name="before"/> for vazio ou contiver valores do tipo
    /// <see cref="float"/>, <see cref="double"/> ou <see cref="decimal"/>.
    /// </exception>
    public static AuditDelta ForDelete(IReadOnlyDictionary<string, object?> before)
    {
        ArgumentNullException.ThrowIfNull(before);

        if (before.Count == 0)
            throw new ArgumentException(
                "O delta de exclusão não pode ser vazio.",
                nameof(before));

        ValidateNoForbiddenNumericTypes(before.Values);

        return new AuditDelta { Kind = AuditDeltaKind.Delete, Before = before };
    }

    // ------------------------------------------------------------------ Equality (value semantics)

    /// <summary>
    /// Igualdade por valor: dois deltas são iguais quando têm o mesmo Kind e o mesmo conteúdo.
    /// Comparação de dicionários é feita por conteúdo (chaves + valores), não por referência.
    /// </summary>
    public bool Equals(AuditDelta? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Kind == other.Kind
            && ObjectDictionaryEquals(After, other.After)
            && ObjectDictionaryEquals(Before, other.Before)
            && ChangesDictionaryEquals(Changes, other.Changes);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(After?.Count ?? -1);
        hash.Add(Before?.Count ?? -1);
        hash.Add(Changes?.Count ?? -1);
        return hash.ToHashCode();
    }

    // ------------------------------------------------------------------ Factory interno (pós-mascaramento)

    /// <summary>
    /// Reconstrói um delta de atualização após mascaramento de PII.
    /// Ao contrário de <see cref="ForUpdate"/>, permite pares onde before == after quando
    /// ambos os valores são o marcador de mascaramento (design §4.3, REQ-004.4).
    /// <para>
    /// Uso restrito ao <see cref="AuditLog.Domain.Services.PiiMasker"/>.
    /// Não use este método para criar deltas de dados reais — use <see cref="ForUpdate"/>.
    /// </para>
    /// </summary>
    internal static AuditDelta ForMaskedUpdate(IReadOnlyDictionary<string, AuditAttributeChange> maskedChanges)
    {
        ArgumentNullException.ThrowIfNull(maskedChanges);

        if (maskedChanges.Count == 0)
            throw new ArgumentException(
                "O delta de atualização mascarado deve conter pelo menos um atributo.",
                nameof(maskedChanges));

        var allValues = maskedChanges.Values.SelectMany(c => new[] { c.Before, c.After });
        ValidateNoForbiddenNumericTypes(allValues);

        return new AuditDelta { Kind = AuditDeltaKind.Update, Changes = maskedChanges };
    }

    // ------------------------------------------------------------------ Guard

    private static void ValidateNoForbiddenNumericTypes(IEnumerable<object?> values)
    {
        foreach (var value in values)
        {
            if (value is float)
                throw new ArgumentException(
                    "Valores do tipo float são proibidos no AuditDelta. " +
                    "Valores monetários devem ser representados em centavos inteiros (long) — DD-005.");

            if (value is double)
                throw new ArgumentException(
                    "Valores do tipo double são proibidos no AuditDelta. " +
                    "Valores monetários devem ser representados em centavos inteiros (long) — DD-005.");

            if (value is decimal)
                throw new ArgumentException(
                    "Valores do tipo decimal são proibidos no AuditDelta. " +
                    "Valores monetários devem ser representados em centavos inteiros (long) — DD-005.");
        }
    }

    // ------------------------------------------------------------------ Helpers de igualdade

    private static bool ObjectDictionaryEquals(
        IReadOnlyDictionary<string, object?>? a,
        IReadOnlyDictionary<string, object?>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var bValue)) return false;
            if (!Equals(value, bValue)) return false;
        }

        return true;
    }

    private static bool ChangesDictionaryEquals(
        IReadOnlyDictionary<string, AuditAttributeChange>? a,
        IReadOnlyDictionary<string, AuditAttributeChange>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;

        foreach (var (key, change) in a)
        {
            if (!b.TryGetValue(key, out var bChange)) return false;
            if (!Equals(change.Before, bChange.Before)) return false;
            if (!Equals(change.After, bChange.After)) return false;
        }

        return true;
    }
}
