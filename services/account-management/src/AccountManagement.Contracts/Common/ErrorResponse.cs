namespace AccountManagement.Contracts.Common;

/// <summary>
/// Resposta de erro padronizada para todos os endpoints do módulo account-management.
///
/// Formato: <c>{ "error", "code", "correlationId" }</c> — rule api-and-contracts.md.
///
/// A mensagem não expõe PII nem detalhes de infraestrutura (design §12, RNF 1.3, Req 9).
///
/// Mapeia: design §8, design §12, rule api-and-contracts.md.
/// </summary>
public sealed record ErrorResponse(
    /// <summary>Mensagem de erro legível por humanos (sem PII).</summary>
    string Error,
    /// <summary>Código de erro do catálogo ACC-ERR (ex.: "ACC-ERR-001").</summary>
    string Code,
    /// <summary>Identificador de correlação da requisição (rastreabilidade — RNF 9).</summary>
    string CorrelationId);
