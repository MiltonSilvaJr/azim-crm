namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para geração e verificação de hash de tokens de convite.
/// O valor em claro do token é computado aqui e nunca persistido (RNF 3, DD-009).
/// </summary>
public interface ITokenHasher
{
    /// <summary>
    /// Gera um token criptograficamente seguro e retorna o par (token em claro, hash).
    /// O token em claro é enviado por e-mail; apenas o hash é persistido.
    /// </summary>
    /// <returns>Tupla com o token em claro e seu hash.</returns>
    (string PlainToken, string Hash) GenerateToken();

    /// <summary>
    /// Computa o hash de um token fornecido em claro.
    /// </summary>
    /// <param name="plainToken">Token em claro recebido no aceite.</param>
    /// <returns>Hash correspondente para comparação com o armazenado.</returns>
    string Hash(string plainToken);
}
