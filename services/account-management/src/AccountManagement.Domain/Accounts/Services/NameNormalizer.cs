using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AccountManagement.Domain.Accounts.ValueObjects;

namespace AccountManagement.Domain.Accounts.Services;

/// <summary>
/// Domain service puro e determinístico responsável por normalizar nomes de contas.
///
/// Algoritmo (design §4.3, DD-005):
/// 1. Normalizar Unicode para forma NFD e remover marcas diacríticas (acentos).
/// 2. Converter para minúsculas (invariante de cultura).
/// 3. Remover pontuação e símbolos não alfanuméricos (mantém letras, dígitos e espaço).
/// 4. Colapsar espaços múltiplos em um único e aplicar trim.
///
/// Propriedades garantidas:
/// - Idempotência: normalize(normalize(x)) == normalize(x) (PBT-01).
/// - Equivalência: nomes que diferem apenas por acento/caixa/espaço/pontuação
///   produzem forma normalizada idêntica (PBT-02).
/// - Determinismo: sem fuzzy matching — dedupe exata sobre a forma normalizada (DD-005).
///
/// Mapeia: design §4.3, Req 1.1, DD-005, PBT-01, PBT-02.
/// </summary>
public sealed class NameNormalizer
{
    private static readonly Regex NonAlphanumericOrSpace = new(
        @"[^\p{L}\p{N}\s]",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex MultipleSpaces = new(
        @"\s+",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Normaliza um nome de conta aplicando o algoritmo canônico do domínio.
    /// </summary>
    /// <param name="name">Nome a normalizar. Nulo ou vazio retorna <see cref="string.Empty"/>.</param>
    /// <returns>Forma normalizada do nome.</returns>
    public string Normalize(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        // Passo 1: Normalizar para NFD e remover marcas diacríticas
        var nfd = name.Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new string(
            nfd.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
               .ToArray());

        // Passo 2: Converter para minúsculas
        var lower = withoutDiacritics.ToLowerInvariant();

        // Passo 3: Remover pontuação e símbolos não alfanuméricos (preservar letras, dígitos, espaço).
        // Substitui por string vazia para que "PAG.AI" → "pagai" (DD-005).
        var lettersDigitsSpaces = NonAlphanumericOrSpace.Replace(lower, string.Empty);

        // Passo 4: Colapsar espaços múltiplos e aplicar trim
        var collapsed = MultipleSpaces.Replace(lettersDigitsSpaces, " ").Trim();

        return collapsed;
    }

    /// <summary>
    /// Cria um <see cref="NormalizedName"/> a partir de um nome bruto,
    /// aplicando a normalização canônica.
    /// </summary>
    /// <param name="name">Nome bruto da conta.</param>
    /// <returns>Objeto de valor <see cref="NormalizedName"/> com a forma normalizada.</returns>
    public NormalizedName NormalizeName(string? name) =>
        NormalizedName.Create(Normalize(name));
}
