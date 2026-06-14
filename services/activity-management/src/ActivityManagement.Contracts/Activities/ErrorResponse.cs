namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de resposta de erro padronizado para todos os endpoints (rule api-and-contracts.md).
/// Mapeia: design §8, design §12, TASK-18.
/// </summary>
/// <param name="Error">Mensagem de erro (sem PII — RNF 7.3).</param>
/// <param name="Code">Código do catálogo ACT-ERR-NNN.</param>
/// <param name="CorrelationId">Identificador de correlação da requisição (RNF 6.1).</param>
public sealed record ErrorResponse(
    string  Error,
    string  Code,
    Guid    CorrelationId);
