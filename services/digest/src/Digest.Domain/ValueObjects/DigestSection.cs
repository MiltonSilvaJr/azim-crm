namespace Digest.Domain.ValueObjects;

/// <summary>
/// Bloco canônico de conteúdo do digest para um usuário.
/// Imutável; sabe se está vazio. Suporta a regra "ao menos um bloco" (Req 4.6).
/// </summary>
/// <param name="Key">Identificador canônico do bloco (ex.: "overdue_activities").</param>
/// <param name="Items">Itens de conteúdo do bloco. Imutável após construção.</param>
public sealed record DigestSection(string Key, IReadOnlyList<string> Items)
{
    /// <summary>
    /// Constrói um <see cref="DigestSection"/> a partir de uma chave e coleção de itens.
    /// </summary>
    public DigestSection(string key, IEnumerable<string> items)
        : this(key, items?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(items)))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
    }

    /// <summary>Retorna <c>true</c> quando o bloco não contém nenhum item.</summary>
    public bool IsEmpty() => Items.Count == 0;
}
