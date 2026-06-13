using System.Text.Json.Serialization;

namespace Authentication.Contracts.Errors;

/// <summary>
/// Resposta de erro padrão do módulo authentication.
///
/// Formato: <c>{ "code": "AUTH-ERR-NNN", "message": "Mensagem segura." }</c>
///
/// Regras (design.md § 12, Req 10.4):
///   - Nunca contém PII (e-mail, nome, identity_uid).
///   - Nunca expõe detalhe interno (stack trace, nome de provider, SQL).
///   - Toda mensagem referencia apenas o catálogo de erros (<see cref="ErrorCatalog"/>).
///
/// Mapeia: TASK-17, design.md § 12, Req 10.4.
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>
    /// Código do catálogo de erros (padrão AUTH-ERR-NNN).
    /// Permite ao cliente identificar o tipo de erro sem expor detalhe interno.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Mensagem orientada ao consumidor. Nunca contém PII nem detalhe de implementação.
    /// Derivada do <see cref="ErrorCatalog"/> correspondente ao <see cref="Code"/>.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Cria uma resposta de erro a partir de um código do catálogo.
    /// </summary>
    /// <param name="code">Código AUTH-ERR-NNN.</param>
    /// <returns>Instância com código e mensagem do catálogo.</returns>
    public static ErrorResponse FromCatalog(string code) =>
        new() { Code = code, Message = ErrorCatalog.GetMessage(code) };
}
