namespace Digest.Domain.ValueObjects;

/// <summary>
/// Coleção de <see cref="DigestSection"/>s compostas para um usuário.
/// Imutável; expõe <see cref="HasContent"/> para verificar se ao menos um bloco não está vazio (Req 4.6).
/// </summary>
public sealed class DigestContent
{
    private readonly IReadOnlyList<DigestSection> _sections;

    /// <summary>
    /// Constrói um <see cref="DigestContent"/> a partir de uma coleção de blocos.
    /// </summary>
    /// <param name="sections">Blocos de conteúdo. Pode ser vazia.</param>
    public DigestContent(IEnumerable<DigestSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        _sections = sections.ToList().AsReadOnly();
    }

    /// <summary>Coleção imutável de blocos de conteúdo.</summary>
    public IReadOnlyList<DigestSection> Sections => _sections;

    /// <summary>
    /// Retorna <c>true</c> quando ao menos um bloco não está vazio.
    /// Utilizado para decidir se o digest deve ser enviado ao usuário (Req 4.6).
    /// </summary>
    public bool HasContent() => _sections.Any(s => !s.IsEmpty());

    /// <summary>
    /// Retorna o bloco com a chave especificada, ou <c>null</c> se não encontrado.
    /// </summary>
    public DigestSection? GetSection(string key) =>
        _sections.FirstOrDefault(s => s.Key == key);
}
