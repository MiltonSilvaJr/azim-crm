namespace Authentication.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que representa o conjunto de memberships de um usuário
/// (mapeamento de unidade de negócio → papel), com carimbo de tempo para TTL de cache.
///
/// Invariantes (design.md § 4.3, Req 5.5):
///   - Sem duplicidade de <c>bu_id</c> nas entradas.
///   - Serializável para cache (Redis).
///   - Imutável após construção; igualdade por valor.
/// </summary>
public sealed class MembershipSet : IEquatable<MembershipSet>
{
    /// <summary>Entradas de membership (bu_id → papel). Somente leitura.</summary>
    public IReadOnlyList<MembershipEntry> Entries { get; }

    /// <summary>Carimbo UTC de quando este conjunto foi carregado/cacheado (para TTL).</summary>
    public DateTimeOffset CachedAt { get; }

    /// <summary>Conjunto de memberships vazio (sem entradas).</summary>
    public static readonly MembershipSet Empty =
        new(Array.Empty<MembershipEntry>(), DateTimeOffset.MinValue);

    private MembershipSet(IReadOnlyList<MembershipEntry> entries, DateTimeOffset cachedAt)
    {
        Entries = entries;
        CachedAt = cachedAt;
    }

    /// <summary>
    /// Cria um <see cref="MembershipSet"/> com as entradas fornecidas.
    /// </summary>
    /// <param name="entries">Entradas de membership (bu_id único por entrada).</param>
    /// <param name="cachedAt">Momento UTC em que este conjunto foi obtido.</param>
    /// <returns>Instância imutável de <see cref="MembershipSet"/>.</returns>
    /// <exception cref="ArgumentException">Lançada quando há bu_id duplicado.</exception>
    public static MembershipSet Create(IEnumerable<MembershipEntry> entries, DateTimeOffset cachedAt)
    {
        var list = entries.ToList();

        var duplicates = list
            .GroupBy(e => e.BuId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new ArgumentException(
                $"MembershipSet não permite bu_id duplicado. Duplicatas: {string.Join(", ", duplicates)}",
                nameof(entries));

        return new MembershipSet(list.AsReadOnly(), cachedAt);
    }

    /// <inheritdoc/>
    public bool Equals(MembershipSet? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return CachedAt == other.CachedAt &&
               Entries.Count == other.Entries.Count &&
               Entries.Zip(other.Entries).All(pair => pair.First == pair.Second);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is MembershipSet other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CachedAt);
        foreach (var entry in Entries)
            hash.Add(entry);
        return hash.ToHashCode();
    }

    /// <summary>Compara dois conjuntos por valor.</summary>
    public static bool operator ==(MembershipSet? left, MembershipSet? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compara dois conjuntos por valor (desigualdade).</summary>
    public static bool operator !=(MembershipSet? left, MembershipSet? right) => !(left == right);
}
