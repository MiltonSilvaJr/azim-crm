namespace AccountManagement.Application.Exceptions;

/// <summary>
/// Exceção lançada quando um contato não é encontrado ou não pertence ao tenant/conta do contexto.
///
/// Corresponde ao erro ACC-ERR-006 (design §12).
/// Não distingue "não existe" de "fora do tenant" para evitar enumeração (Req 9, PBT-04).
///
/// Mapeia: design §12, Req 10, ACC-ERR-006.
/// </summary>
public sealed class ContactNotFoundException : Exception
{
    /// <summary>Inicializa com a mensagem padrão do catálogo de erros.</summary>
    public ContactNotFoundException()
        : base("Contato não encontrado.")
    {
    }

    /// <summary>Inicializa com mensagem personalizada.</summary>
    /// <param name="message">Mensagem descritiva.</param>
    public ContactNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Inicializa com mensagem e exceção interna.</summary>
    public ContactNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
