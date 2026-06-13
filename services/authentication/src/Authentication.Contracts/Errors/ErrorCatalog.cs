namespace Authentication.Contracts.Errors;

/// <summary>
/// Catálogo centralizado de erros do módulo authentication.
///
/// Contém os 16 códigos AUTH-ERR-001..090 definidos em design.md § 12.
///
/// Regras (Req 10.4, DD-006):
///   - Nenhuma mensagem expõe PII (e-mail, identity_uid, nome de usuário).
///   - Nenhuma mensagem expõe detalhe de implementação (Firebase, SQL, Redis, stack trace).
///   - Mensagens são orientadas ao consumidor final — claras, sem revelar existência de conta.
///   - Erros de anti-enumeração (reset, login) usam mensagens genéricas idênticas.
///
/// Mapeia: TASK-17, design.md § 12, Req 10.4, DD-006.
/// </summary>
public static class ErrorCatalog
{
    /// <summary>
    /// Dicionário imutável de todos os códigos e mensagens do catálogo.
    /// Chave: código AUTH-ERR-NNN. Valor: mensagem orientada ao consumidor.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Messages { get; } =
        new Dictionary<string, string>
        {
            // =========================================================================
            // Erros de sessão / token (401, 403) — design.md § 12
            // =========================================================================

            /// <summary>Token ausente ou malformado (estado anonymous).</summary>
            ["AUTH-ERR-001"] = "Sessão inválida ou expirada.",

            /// <summary>Assinatura inválida (JWKS).</summary>
            ["AUTH-ERR-002"] = "Sessão inválida ou expirada.",

            /// <summary>Token expirado (estado expired).</summary>
            ["AUTH-ERR-003"] = "Sessão inválida ou expirada.",

            /// <summary>Token revogado (logout/revogação global) ou tenant divergente.</summary>
            ["AUTH-ERR-004"] = "Sessão inválida ou expirada.",

            /// <summary>identity_uid válido sem user_id ativo no tenant (Req 5.4) — 403.</summary>
            ["AUTH-ERR-005"] = "Acesso negado.",

            // =========================================================================
            // Erros de tenant (404) — design.md § 12
            // =========================================================================

            /// <summary>Slug inexistente ou inativo (MSG-002, Req 1.2) — 404.</summary>
            ["AUTH-ERR-010"] = "Workspace não encontrado.",

            // =========================================================================
            // Erros padronizados do frontend (401) — design.md § 12, DD-004
            // =========================================================================

            /// <summary>Credencial inválida no login (MSG-001) — padrão para frontend.</summary>
            ["AUTH-ERR-011"] = "E-mail ou senha incorretos.",

            /// <summary>Conta com convite pendente (MSG-004, Req 2.4).</summary>
            ["AUTH-ERR-012"] = "Verifique o e-mail de convite para ativar sua conta.",

            /// <summary>Google não associado ao tenant (MSG-006, Req 3.3).</summary>
            ["AUTH-ERR-013"] = "Use o e-mail correto ou solicite um convite.",

            // =========================================================================
            // Erros de serviço (503) — design.md § 12
            // =========================================================================

            /// <summary>Circuit breaker aberto / IdP indisponível (RNF 9.1) — 503.</summary>
            ["AUTH-ERR-020"] = "Serviço de autenticação indisponível. Tente novamente.",

            // =========================================================================
            // Erros de convite (409, 422, 502, 410) — design.md § 12
            // =========================================================================

            /// <summary>E-mail já cadastrado e ativo (MSG-007, Req 7.6) — 409.</summary>
            ["AUTH-ERR-030"] = "Não foi possível concluir o convite.",

            /// <summary>Papel ou BU inexistentes no tenant (Req 7) — 422.</summary>
            ["AUTH-ERR-031"] = "Papel ou unidade de negócio inválidos.",

            /// <summary>Falha de entrega do e-mail de convite (MSG-009, Req 7.2) — 502.</summary>
            ["AUTH-ERR-032"] = "Convite criado, mas o e-mail não pôde ser enviado.",

            /// <summary>Link expirado ou já consumido (MSG-008, Req 7.4/7.5, PBT-05) — 410.</summary>
            ["AUTH-ERR-033"] = "Link de convite inválido ou expirado. Solicite um novo.",

            // =========================================================================
            // Rate limiting (429) — design.md § 12
            // =========================================================================

            /// <summary>
            /// Excesso de tentativas por IP/tenant (RNF 8) — 429.
            /// Mensagem genérica que não revela existência de conta (RNF 8.2).
            /// </summary>
            ["AUTH-ERR-040"] = "Muitas tentativas. Tente novamente em instantes.",

            // =========================================================================
            // Erros internos inesperados (500) — design.md § 12
            // =========================================================================

            /// <summary>Falha de tradução/contrato inesperado do IdP (Req 6.5) — 500.</summary>
            ["AUTH-ERR-090"] = "Erro interno de autenticação."
        };

    /// <summary>
    /// Retorna a mensagem correspondente ao código do catálogo.
    ///
    /// Para código desconhecido, retorna a mensagem genérica de erro interno
    /// (<c>AUTH-ERR-090</c>) sem expor o código inválido ao consumidor.
    /// </summary>
    /// <param name="code">Código AUTH-ERR-NNN.</param>
    /// <returns>Mensagem segura orientada ao consumidor.</returns>
    public static string GetMessage(string code) =>
        Messages.TryGetValue(code, out var message)
            ? message
            : Messages["AUTH-ERR-090"];
}
