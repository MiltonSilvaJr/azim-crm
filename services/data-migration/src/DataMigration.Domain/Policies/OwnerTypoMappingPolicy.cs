namespace DataMigration.Domain.Policies;

/// <summary>
/// Policy que corrige typos conhecidos no nome do responsável (owner) via mapa configurável.
///
/// Exemplo: "Miilton" → "Milton" (case-sensitive na chave).
///
/// O mapa é fornecido via configuração (não hardcoded no domínio) para permitir
/// ajuste sem redeployment.
///
/// Rastreia: design §4.6, Req 10.3, TASK-07.
/// </summary>
public sealed class OwnerTypoMappingPolicy
{
    private readonly IReadOnlyDictionary<string, string> _mapping;

    /// <summary>
    /// Cria a policy com o mapa de typos → nomes corretos.
    /// </summary>
    /// <param name="mapping">
    /// Dicionário case-sensitive: chave = typo, valor = nome correto.
    /// </param>
    public OwnerTypoMappingPolicy(IReadOnlyDictionary<string, string> mapping)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
    }

    /// <summary>
    /// Tenta resolver um typo no nome do owner.
    /// </summary>
    /// <param name="ownerName">Nome do responsável da planilha.</param>
    /// <param name="correctedName">Nome corrigido, ou <c>null</c> se não há mapeamento.</param>
    /// <returns><c>true</c> se o typo foi encontrado e corrigido.</returns>
    public bool TryResolve(string? ownerName, out string? correctedName)
    {
        if (!string.IsNullOrWhiteSpace(ownerName) &&
            _mapping.TryGetValue(ownerName, out var resolved))
        {
            correctedName = resolved;
            return true;
        }

        correctedName = null;
        return false;
    }
}
