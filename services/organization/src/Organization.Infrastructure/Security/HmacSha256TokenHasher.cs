using System.Security.Cryptography;
using System.Text;
using Organization.Application.Ports;

namespace Organization.Infrastructure.Security;

/// <summary>
/// Implementação de <see cref="ITokenHasher"/> usando HMAC-SHA256 com pepper via <see cref="ISecretProvider"/>.
/// O pepper é obtido do Secret Manager e nunca persiste em código ou logs (design §10, DD-007, RNF 3).
/// Substitui o <c>Sha256TokenHasher</c> MVP da Onda 6 — TASK-26.
/// </summary>
public sealed class HmacSha256TokenHasher : ITokenHasher
{
    /// <summary>Nome da chave do pepper no Secret Manager.</summary>
    public const string PepperSecretName = "organization_token_pepper";

    private readonly ISecretProvider _secretProvider;

    /// <summary>Inicializa o hasher com o provedor de segredos.</summary>
    public HmacSha256TokenHasher(ISecretProvider secretProvider)
    {
        _secretProvider = secretProvider;
    }

    /// <inheritdoc/>
    public (string PlainToken, string Hash) GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var plain = Convert.ToBase64String(bytes);
        // Hash síncrono — a versão assíncrona requer refactoring de interface (fora do escopo MVP)
        // O pepper é carregado lazily na primeira chamada a Hash
        var hash = Hash(plain);
        return (plain, hash);
    }

    /// <inheritdoc/>
    public string Hash(string plainToken)
    {
        // Carrega pepper de forma síncrona; em produção o Secret Manager faz cache da resposta
        // O uso de .GetAwaiter().GetResult() é aceitável aqui porque:
        // 1. ITokenHasher.Hash é parte de uma interface síncrona herdada do MVP
        // 2. O pepper é normalmente cacheado pelo ISecretProvider
        // 3. Chamadas são raras (apenas na emissão e aceite de convites)
        var pepper = _secretProvider.GetSecretAsync(PepperSecretName).GetAwaiter().GetResult();

        var pepperBytes = Encoding.UTF8.GetBytes(pepper);
        var tokenBytes = Encoding.UTF8.GetBytes(plainToken);

        var hmacBytes = HMACSHA256.HashData(pepperBytes, tokenBytes);
        return Convert.ToHexString(hmacBytes).ToLowerInvariant();
    }
}
