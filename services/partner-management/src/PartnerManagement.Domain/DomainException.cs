namespace PartnerManagement.Domain;

/// <summary>
/// Classe base para exceções de domínio do módulo partner-management.
/// Todas as exceções de regra de negócio herdam desta classe.
/// Mensagens públicas não expõem PII (RNF 4, design §12).
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>Código de erro padronizado do catálogo (ex.: PM-ERR-001).</summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="DomainException"/>.
    /// </summary>
    /// <param name="errorCode">Código de erro do catálogo (design §12).</param>
    /// <param name="message">Mensagem pública sem PII.</param>
    protected DomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
