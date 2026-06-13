namespace Authentication.Application.Ports.Exceptions;

/// <summary>
/// Exceção lançada quando o provedor de identidade retorna erro ou falha.
///
/// Todas as exceções do Firebase Admin SDK são capturadas pelo adapter em Infrastructure
/// e mapeadas para esta exceção com o código do catálogo correspondente (Req 6.5).
/// Nenhum tipo do Firebase SDK propaga para além do adapter.
///
/// Mapeia: Req 6.5, design.md § 6.4, DD-001.
/// </summary>
public sealed class IdentityProviderException : Exception
{
    /// <summary>Código de erro do catálogo (ex.: "AUTH-ERR-002", "AUTH-ERR-020").</summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="IdentityProviderException"/>.
    /// </summary>
    /// <param name="errorCode">Código do catálogo de erros (design.md § 12).</param>
    /// <param name="message">Mensagem interna (não exposta ao consumidor).</param>
    public IdentityProviderException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="IdentityProviderException"/> com causa raiz.
    /// </summary>
    /// <param name="errorCode">Código do catálogo de erros (design.md § 12).</param>
    /// <param name="message">Mensagem interna (não exposta ao consumidor).</param>
    /// <param name="innerException">Exceção original do adapter (nunca propagada ao caller).</param>
    public IdentityProviderException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
