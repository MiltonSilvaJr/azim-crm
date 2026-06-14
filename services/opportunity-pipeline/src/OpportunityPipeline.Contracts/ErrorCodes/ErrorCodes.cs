using System.Text.Json.Serialization;

namespace OpportunityPipeline.Contracts.ErrorCodes;

/// <summary>
/// Catálogo de erros do módulo opportunity-pipeline.
/// Códigos OP-ERR-001..017 — estáveis e rastreáveis.
/// Nenhum código expõe informação sensível ou de outro tenant.
/// Mapeia: design §12, TASK-19.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Campos obrigatórios ausentes para criar oportunidade (account_id, bu_id, stage_id, title).</summary>
    public const string OP_ERR_001 = "OP-ERR-001";

    /// <summary>OPPORTUNITY_OWNER_REQUIRED — owner obrigatório ou inválido na BU.</summary>
    public const string OP_ERR_002 = "OP-ERR-002";

    /// <summary>Canal de origem (origin_channel_id) obrigatório.</summary>
    public const string OP_ERR_003 = "OP-ERR-003";

    /// <summary>Parceiro obrigatório para canal de origem do tipo Parceiro.</summary>
    public const string OP_ERR_004 = "OP-ERR-004";

    /// <summary>Data de fechamento esperada obrigatória a partir do estágio "Proposta Enviada".</summary>
    public const string OP_ERR_005 = "OP-ERR-005";

    /// <summary>Motivo de perda (loss_reason_id) obrigatório ao encerrar como perdida.</summary>
    public const string OP_ERR_006 = "OP-ERR-006";

    /// <summary>Probabilidade fora do intervalo permitido [0, 100].</summary>
    public const string OP_ERR_007 = "OP-ERR-007";

    /// <summary>Reabertura não permitida para o papel atual (Vendedor/Viewer). HTTP 403.</summary>
    public const string OP_ERR_008 = "OP-ERR-008";

    /// <summary>Exatamente um contato principal é obrigatório quando há contatos vinculados.</summary>
    public const string OP_ERR_009 = "OP-ERR-009";

    /// <summary>Conta (account_id) não encontrada no tenant.</summary>
    public const string OP_ERR_010 = "OP-ERR-010";

    /// <summary>Título não pode conter PII de contato em texto livre.</summary>
    public const string OP_ERR_011 = "OP-ERR-011";

    /// <summary>duracao_meses obrigatório quando há valor mensal maior que zero.</summary>
    public const string OP_ERR_012 = "OP-ERR-012";

    /// <summary>Transição de estágio inválida conforme máquina de estados.</summary>
    public const string OP_ERR_013 = "OP-ERR-013";

    /// <summary>valor_fixo e percentuais (pct_setup, pct_recorrente) são mutuamente excludentes.</summary>
    public const string OP_ERR_014 = "OP-ERR-014";

    /// <summary>Parceiro (partner_id) não encontrado no tenant.</summary>
    public const string OP_ERR_015 = "OP-ERR-015";

    /// <summary>Contato não pertence à conta da oportunidade.</summary>
    public const string OP_ERR_016 = "OP-ERR-016";

    /// <summary>Comissão obrigatória para ganhar (canal Parceiro) — condicional à política VAL-07/DD-007.</summary>
    public const string OP_ERR_017 = "OP-ERR-017";

    /// <summary>Mapa de código de erro para mensagem legível (pt-BR).</summary>
    public static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<string, string>
    {
        [OP_ERR_001] = "Campos obrigatórios ausentes para criar oportunidade.",
        [OP_ERR_002] = "Owner obrigatório ou inválido para a BU informada.",
        [OP_ERR_003] = "Canal de origem obrigatório.",
        [OP_ERR_004] = "Parceiro obrigatório para canal do tipo Parceiro.",
        [OP_ERR_005] = "Data de fechamento esperada obrigatória neste estágio.",
        [OP_ERR_006] = "Motivo de perda obrigatório.",
        [OP_ERR_007] = "Probabilidade fora do intervalo [0, 100].",
        [OP_ERR_008] = "Reabertura não permitida para este papel.",
        [OP_ERR_009] = "Exatamente um contato principal é obrigatório.",
        [OP_ERR_010] = "Conta não encontrada.",
        [OP_ERR_011] = "Título não pode conter PII.",
        [OP_ERR_012] = "Duração em meses obrigatória quando há valor mensal.",
        [OP_ERR_013] = "Transição de estágio inválida.",
        [OP_ERR_014] = "valor_fixo e percentuais são mutuamente excludentes.",
        [OP_ERR_015] = "Parceiro não encontrado.",
        [OP_ERR_016] = "Contato não pertence à conta da oportunidade.",
        [OP_ERR_017] = "Comissão obrigatória para ganhar oportunidade de canal Parceiro.",
    };
}

/// <summary>
/// ProblemDetails estendido com campos do módulo opportunity-pipeline.
/// Inclui error_code (OP-ERR-*) e correlation_id para rastreabilidade.
/// Nunca expõe dados sensíveis, PII ou informações de infraestrutura.
/// Mapeia: design §8, P10 (API-first), RNF 10.
/// </summary>
public sealed class OpProblemDetails
{
    /// <summary>Título HTTP padrão (ex.: "Unprocessable Entity").</summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Código HTTP de status.</summary>
    [JsonPropertyName("status")]
    public int Status { get; init; }

    /// <summary>Descrição legível do problema (pt-BR).</summary>
    [JsonPropertyName("detail")]
    public string Detail { get; init; } = string.Empty;

    /// <summary>Código de erro do catálogo OP-ERR-* para diagnóstico.</summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

    /// <summary>Correlation ID da request para rastreabilidade (RNF 10).</summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }

    /// <summary>Campo que gerou o erro de validação (quando aplicável).</summary>
    [JsonPropertyName("field")]
    public string? Field { get; init; }
}
