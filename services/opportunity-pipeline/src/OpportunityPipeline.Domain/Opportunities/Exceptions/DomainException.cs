namespace OpportunityPipeline.Domain.Opportunities.Exceptions;

/// <summary>
/// Exceção base para todas as violações de invariante de domínio do módulo opportunity-pipeline.
/// Subclasses mapeiam para códigos de erro OP-ERR-* do catálogo (design §12).
/// </summary>
public class DomainException : Exception
{
    /// <summary>Inicializa com a mensagem de violação de invariante.</summary>
    public DomainException(string message)
        : base(message)
    {
    }

    /// <summary>Inicializa com mensagem e causa raiz.</summary>
    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
