namespace AccountManagement.Application.Exceptions;

/// <summary>
/// Exceção lançada quando um usuário tenta acessar PII de contato sem o papel adequado,
/// ou quando tenta executar o esquecimento sem ser Tenant Admin.
///
/// Corresponde ao erro ACC-ERR-008 (design §12).
/// A mensagem não revela a existência do recurso (anti-enumeração — Req 9, PBT-04).
///
/// Mapeia: design §12, Req 9, RNF 6, ACC-ERR-008.
/// </summary>
public sealed class PiiAccessDeniedException : Exception
{
    /// <summary>Inicializa com a mensagem padrão do catálogo de erros.</summary>
    public PiiAccessDeniedException()
        : base("Acesso negado.")
    {
    }

    /// <summary>Inicializa com mensagem personalizada.</summary>
    /// <param name="message">Mensagem descritiva.</param>
    public PiiAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>Inicializa com mensagem e exceção interna.</summary>
    public PiiAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
