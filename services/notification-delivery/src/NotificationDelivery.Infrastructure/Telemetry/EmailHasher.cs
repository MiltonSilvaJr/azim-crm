using System.Security.Cryptography;
using System.Text;

namespace NotificationDelivery.Infrastructure.Telemetry;

/// <summary>
/// Gera um identificador pseudônimo determinístico para o e-mail do destinatário.
///
/// Usado na telemetria para substituir o endereço de e-mail (PII) por um hash
/// truncado que permite diagnóstico sem violar RNF 4 e DD-008.
///
/// Algoritmo: SHA-256 sobre o e-mail normalizado (lowercase trim),
/// truncado para <see cref="HashLength"/> caracteres hex.
///
/// Invariantes (RNF 4, DD-008, PBT-03 base estrutural):
/// <list type="bullet">
///   <item><description>Hash é determinístico: <c>Hash(x) == Hash(x)</c> sempre.</description></item>
///   <item><description>Hash não contém fragmento do e-mail original (não há reversibilidade prática).</description></item>
///   <item><description>Dois e-mails distintos (não iguais) produzem hashes distintos na prática (colisão improvável).</description></item>
///   <item><description>A classe é stateless e thread-safe.</description></item>
/// </list>
/// </summary>
public static class EmailHasher
{
    /// <summary>
    /// Comprimento do hash hex truncado (12 chars = 48 bits de entropia).
    /// Suficiente para diagnóstico; insuficiente para reversão prática (RNF 4, DD-008).
    /// </summary>
    public const int HashLength = 12;

    /// <summary>
    /// Prefixo do identificador mascarado para facilitar identificação em logs.
    /// </summary>
    public const string HashPrefix = "email#";

    /// <summary>
    /// Gera o identificador pseudônimo para uso em telemetria (logs, métricas, traces).
    ///
    /// <para>O retorno é um hash hex truncado no formato <c>email#xxxxxxxxxxxx</c>.</para>
    ///
    /// <para>Nunca deve ser usado para comparação de identidade — serve apenas para
    /// correlacionar eventos de um mesmo destinatário sem expor o e-mail em claro.</para>
    /// </summary>
    /// <param name="email">Endereço de e-mail do destinatário (PII). Nunca logado.</param>
    /// <returns>Identificador pseudônimo para telemetria (sem PII).</returns>
    /// <exception cref="ArgumentNullException">Quando <paramref name="email"/> for nulo.</exception>
    public static string Hash(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        // Normalização: lowercase + trim (idempotente para o mesmo endereço)
        var normalized = email.Trim().ToLowerInvariant();
        var inputBytes = Encoding.UTF8.GetBytes(normalized);

        // SHA-256 → truncar para HashLength chars hex
        var hashBytes = SHA256.HashData(inputBytes);
        var hexFull = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var truncated = hexFull[..HashLength];

        return $"{HashPrefix}{truncated}";
    }

    /// <summary>
    /// Retorna <c>true</c> se o valor fornecido parece ser um hash gerado por <see cref="Hash"/>.
    /// Usado para detectar se PII já foi mascarado antes de logar.
    /// </summary>
    public static bool IsHashed(string? value) =>
        value?.StartsWith(HashPrefix, StringComparison.Ordinal) == true;
}
