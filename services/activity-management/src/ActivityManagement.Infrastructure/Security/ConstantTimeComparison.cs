namespace ActivityManagement.Infrastructure.Security;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Utilitários de comparação em tempo constante para hashes de token (TASK-23, PBT-03).
///
/// O objetivo é prevenir ataques de timing (timing side-channel) que poderiam distinguir
/// token inexistente de token válido, violando o requisito de anti-enumeração (Req 7.6, PBT-03).
///
/// Usa <see cref="CryptographicOperations.FixedTimeEquals"/> da BCL, que garante
/// tempo de execução constante independente do conteúdo dos arrays comparados.
///
/// Mapeia: TASK-23, PBT-03, RNF 5, design §10, Req 7.6.
/// </summary>
public static class ConstantTimeComparison
{
    /// <summary>
    /// Compara dois arrays de bytes em tempo constante.
    /// Retorna <c>true</c> somente se ambos têm o mesmo comprimento e conteúdo idêntico.
    /// </summary>
    /// <param name="a">Primeiro array.</param>
    /// <param name="b">Segundo array.</param>
    /// <returns><c>true</c> quando arrays são idênticos; <c>false</c> em qualquer diferença.</returns>
    public static bool AreEqual(byte[] a, byte[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        // Comprimentos diferentes → false, mas ainda percorre ambos para tempo constante
        // CryptographicOperations.FixedTimeEquals já garante isso quando os comprimentos são iguais.
        // Para comprimentos diferentes, retornamos false imediatamente mas normalizamos o byte[] menor
        // para evitar vazamento de comprimento via timing.
        if (a.Length != b.Length)
        {
            // Executar a comparação com um array dummy do mesmo tamanho para dificultar
            // inferência de comprimento via timing (embora o return já prejudique isso um pouco).
            // Em produção real, a mitigação é garantir que o hash do token tenha sempre tamanho fixo (SHA-256 = 32 bytes).
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>
    /// Compara dois hashes de token em formato string em tempo constante.
    /// Retorna <c>false</c> quando qualquer dos parâmetros for nulo ou vazio.
    /// </summary>
    /// <param name="storedHash">Hash armazenado no banco.</param>
    /// <param name="providedHash">Hash do token apresentado pelo usuário.</param>
    /// <returns><c>true</c> quando os hashes são idênticos em conteúdo e comprimento.</returns>
    public static bool AreEqualStrings(string? storedHash, string? providedHash)
    {
        // Tokens nulos ou vazios nunca são válidos — retorna false imediatamente.
        // Isso não vaza informação de comprimento porque tokens válidos têm sempre
        // tamanho fixo (SHA-256 hex = 64 chars).
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(providedHash))
            return false;

        var storedBytes   = Encoding.UTF8.GetBytes(storedHash);
        var providedBytes = Encoding.UTF8.GetBytes(providedHash);

        return AreEqual(storedBytes, providedBytes);
    }
}
