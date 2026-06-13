namespace Authentication.Application.Ports.Exceptions;

/// <summary>
/// Exceção lançada quando o acesso ao gerenciador de segredos falha.
///
/// Detalhes internos (nome do segredo, resposta do Secret Manager) nunca são
/// propagados ao chamador (RNF 7, DD-005).
///
/// Mapeia: RNF 7, design.md § 6.6, § 6.8, DD-005.
/// </summary>
public sealed class SecretProviderException : Exception
{
    /// <summary>
    /// Inicializa uma nova instância de <see cref="SecretProviderException"/>.
    /// </summary>
    /// <param name="message">Descrição interna da falha (não exposta ao consumidor).</param>
    public SecretProviderException(string message) : base(message) { }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="SecretProviderException"/> com causa raiz.
    /// </summary>
    /// <param name="message">Descrição interna da falha.</param>
    /// <param name="innerException">Exceção original do adapter do Secret Manager.</param>
    public SecretProviderException(string message, Exception innerException)
        : base(message, innerException) { }
}
