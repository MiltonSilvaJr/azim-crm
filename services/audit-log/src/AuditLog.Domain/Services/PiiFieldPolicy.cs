namespace AuditLog.Domain.Services;

/// <summary>
/// Implementação padrão de <see cref="IPiiFieldPolicy"/>.
/// Contém o mapeamento inicial de campos PII por tipo de entidade.
/// <para>
/// Política inicial (REQ-004.1): <c>Contact → [name, email, phone]</c>.
/// </para>
/// <para>
/// Extensão sem alterar <see cref="PiiMasker"/>: instancie com um dicionário customizado
/// ou registre uma nova implementação de <see cref="IPiiFieldPolicy"/> via injeção de dependência.
/// </para>
/// </summary>
public sealed class PiiFieldPolicy : IPiiFieldPolicy
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> DefaultMappings =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Contact"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "name", "email", "phone" }
        };

    private static readonly IReadOnlySet<string> EmptySet =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _mappings;

    /// <summary>
    /// Cria uma política com o mapeamento padrão: <c>Contact → [name, email, phone]</c>.
    /// </summary>
    public PiiFieldPolicy() : this(DefaultMappings) { }

    /// <summary>
    /// Cria uma política com um mapeamento customizado.
    /// </summary>
    /// <param name="mappings">Mapeamento de <c>entity_type</c> para campos PII.</param>
    /// <exception cref="ArgumentNullException">Se <paramref name="mappings"/> for nulo.</exception>
    public PiiFieldPolicy(IReadOnlyDictionary<string, IReadOnlySet<string>> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        _mappings = mappings;
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> GetPiiFields(string entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return _mappings.TryGetValue(entityType, out var fields) ? fields : EmptySet;
    }
}
