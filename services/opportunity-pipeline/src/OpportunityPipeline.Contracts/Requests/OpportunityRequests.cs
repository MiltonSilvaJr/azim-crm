using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OpportunityPipeline.Contracts.Requests;

/// <summary>
/// Request para criar oportunidade (POST /opportunities).
/// Money trafega em centavos inteiros (long). Nunca decimal nos campos de valor.
/// Mapeia: design §8, Req 1..4, OP-ERR-001..004, RNF 11.1, TASK-19.
/// </summary>
public sealed class CreateOpportunityRequest
{
    /// <summary>ID da conta (account_id) — obrigatório (OP-ERR-001/010).</summary>
    [Required]
    [JsonPropertyName("account_id")]
    public Guid AccountId { get; init; }

    /// <summary>ID da BU — obrigatório (OP-ERR-001).</summary>
    [Required]
    [JsonPropertyName("bu_id")]
    public Guid BuId { get; init; }

    /// <summary>ID do estágio inicial — obrigatório (OP-ERR-001).</summary>
    [Required]
    [JsonPropertyName("stage_id")]
    public Guid StageId { get; init; }

    /// <summary>ID do canal de origem — obrigatório (OP-ERR-003).</summary>
    [Required]
    [JsonPropertyName("origin_channel_id")]
    public Guid OriginChannelId { get; init; }

    /// <summary>ID do proprietário da oportunidade — obrigatório (OP-ERR-002).</summary>
    [Required]
    [JsonPropertyName("owner_id")]
    public Guid OwnerId { get; init; }

    /// <summary>Título da oportunidade — obrigatório, sem PII (INV-13, OP-ERR-001/011).</summary>
    [Required]
    [MaxLength(500)]
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>Valor de setup em centavos (long) — padrão 0. Nunca decimal (RNF 11).</summary>
    [JsonPropertyName("valor_setup")]
    public long ValorSetup { get; init; }

    /// <summary>Valor mensal em centavos (long) — padrão 0. Nunca decimal (RNF 11).</summary>
    [JsonPropertyName("valor_mensal")]
    public long ValorMensal { get; init; }

    /// <summary>Duração em meses — obrigatório quando valor_mensal > 0 (OP-ERR-012).</summary>
    [JsonPropertyName("duracao_meses")]
    public int DuracaoMeses { get; init; }

    /// <summary>Probabilidade [0, 100] — null usa default do estágio (OP-ERR-007).</summary>
    [JsonPropertyName("probabilidade")]
    public int? Probabilidade { get; init; }

    /// <summary>Data de fechamento esperada (obrigatória em estágios avançados, OP-ERR-005).</summary>
    [JsonPropertyName("expected_close_date")]
    public DateOnly? ExpectedCloseDate { get; init; }

    /// <summary>Notas livres — sem PII de contato (INV-13).</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>ID do parceiro — obrigatório quando canal é Parceiro (OP-ERR-004).</summary>
    [JsonPropertyName("partner_id")]
    public Guid? PartnerId { get; init; }
}

/// <summary>
/// Request para editar campos permitidos de uma oportunidade (PATCH /opportunities/{id}).
/// Todos os campos são opcionais — apenas os informados são atualizados.
/// Mapeia: design §8, Req 1, OP-ERR-002,007,012, TASK-19.
/// </summary>
public sealed class UpdateOpportunityRequest
{
    /// <summary>Novo owner_id — null mantém o atual (OP-ERR-002).</summary>
    [JsonPropertyName("owner_id")]
    public Guid? OwnerId { get; init; }

    /// <summary>Novo título — null mantém o atual (OP-ERR-001/011).</summary>
    [MaxLength(500)]
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Novo valor de setup em centavos — null mantém o atual.</summary>
    [JsonPropertyName("valor_setup")]
    public long? ValorSetup { get; init; }

    /// <summary>Novo valor mensal em centavos — null mantém o atual.</summary>
    [JsonPropertyName("valor_mensal")]
    public long? ValorMensal { get; init; }

    /// <summary>Nova duração em meses — null mantém o atual.</summary>
    [JsonPropertyName("duracao_meses")]
    public int? DuracaoMeses { get; init; }

    /// <summary>Nova probabilidade [0, 100] — null mantém o atual (OP-ERR-007).</summary>
    [JsonPropertyName("probabilidade")]
    public int? Probabilidade { get; init; }

    /// <summary>Nova data de fechamento esperada — null mantém a atual.</summary>
    [JsonPropertyName("expected_close_date")]
    public DateOnly? ExpectedCloseDate { get; init; }

    /// <summary>Notas livres atualizadas — sem PII (INV-13).</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Request para mover estágio (PATCH /opportunities/{id}/stage).
/// Mapeia: design §8, Req 5, OP-ERR-005,013, TASK-19.
/// </summary>
public sealed class MoveStageRequest
{
    /// <summary>ID do estágio de destino — obrigatório.</summary>
    [Required]
    [JsonPropertyName("stage_id")]
    public Guid StageId { get; init; }

    /// <summary>Data de fechamento esperada (obrigatória em alguns estágios, OP-ERR-005).</summary>
    [JsonPropertyName("expected_close_date")]
    public DateOnly? ExpectedCloseDate { get; init; }
}

/// <summary>
/// Request para encerrar oportunidade como ganha (POST /opportunities/{id}/win).
/// Snapshot de comissão é criado atomicamente.
/// Mapeia: design §8, Req 14, DD-002, DD-007/VAL-07, OP-ERR-013,017, TASK-19.
/// </summary>
public sealed class WinOpportunityRequest
{
    /// <summary>
    /// Confirmação explícita do usuário quando há comissão em branco (VAL-07).
    /// Necessário quando commission_required_on_win = false e parceiro sem comissão.
    /// </summary>
    [JsonPropertyName("confirm_commission_blank")]
    public bool ConfirmCommissionBlank { get; init; }
}

/// <summary>
/// Request para encerrar oportunidade como perdida (POST /opportunities/{id}/lose).
/// Mapeia: design §8, Req 10, OP-ERR-006,013, TASK-19.
/// </summary>
public sealed class LoseOpportunityRequest
{
    /// <summary>ID do motivo de perda — obrigatório (OP-ERR-006).</summary>
    [Required]
    [JsonPropertyName("loss_reason_id")]
    public Guid LossReasonId { get; init; }

    /// <summary>Notas sobre a perda — sem PII (INV-13).</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Request para reabrir oportunidade encerrada (POST /opportunities/{id}/reopen).
/// Apenas GestorBU / TenantAdmin (OP-ERR-008).
/// Mapeia: design §8, Req 15, TASK-19.
/// </summary>
public sealed class ReopenOpportunityRequest
{
    /// <summary>Justificativa da reabertura (opcional, auditada).</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Request para definir ou atualizar comissão de parceiro (PUT /opportunities/{id}/partner-commission).
/// Rejeita se snapshot já existe (409 Conflict).
/// Mapeia: design §8, Req 11, OP-ERR-004,014,015, TASK-19.
/// </summary>
public sealed class SetPartnerCommissionRequest
{
    /// <summary>ID do parceiro — obrigatório (OP-ERR-004/015).</summary>
    [Required]
    [JsonPropertyName("partner_id")]
    public Guid PartnerId { get; init; }

    /// <summary>Papel do parceiro (Indicador, Revendedor, Distribuidor, Integrador).</summary>
    [Required]
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>Percentual sobre setup [0, 100] — mutuamente excludente com valor_fixo (OP-ERR-014).</summary>
    [JsonPropertyName("pct_setup")]
    public decimal PctSetup { get; init; }

    /// <summary>Percentual sobre recorrência [0, 100] — mutuamente excludente com valor_fixo.</summary>
    [JsonPropertyName("pct_recorrente")]
    public decimal PctRecorrente { get; init; }

    /// <summary>Meses comissionados na recorrência.</summary>
    [JsonPropertyName("meses_comissionados")]
    public int MesesComissionados { get; init; }

    /// <summary>Valor fixo em centavos — mutuamente excludente com percentuais (OP-ERR-014).</summary>
    [JsonPropertyName("valor_fixo")]
    public long ValorFixo { get; init; }

    /// <summary>Quando true, pré-preenche defaults do parceiro onde campos estão zerados.</summary>
    [JsonPropertyName("use_partner_defaults")]
    public bool UsePartnerDefaults { get; init; }
}

/// <summary>
/// Request para vincular contato à oportunidade (POST /opportunities/{id}/contacts).
/// Mapeia: design §8, Req 16, OP-ERR-009,016, TASK-19.
/// </summary>
public sealed class LinkContactRequest
{
    /// <summary>ID do contato (da conta vinculada à oportunidade) — obrigatório (OP-ERR-016).</summary>
    [Required]
    [JsonPropertyName("contact_id")]
    public Guid ContactId { get; init; }

    /// <summary>Define se este contato é o principal (INV-11).</summary>
    [JsonPropertyName("is_primary")]
    public bool IsPrimary { get; init; }
}

/// <summary>
/// Request para salvar filtro de lista (POST /opportunities/filters).
/// Mapeia: design §8, Req 19, TASK-19.
/// </summary>
public sealed class SaveFilterRequest
{
    /// <summary>Nome do filtro salvo — único por usuário/tenant.</summary>
    [Required]
    [MaxLength(200)]
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Critérios do filtro em formato JSON estruturado.</summary>
    [Required]
    [JsonPropertyName("criteria")]
    public FilterCriteria Criteria { get; init; } = new();
}

/// <summary>Critérios de filtro para listagem de oportunidades.</summary>
public sealed class FilterCriteria
{
    [JsonPropertyName("owner_id")]
    public Guid? OwnerId { get; init; }

    [JsonPropertyName("origin_channel_id")]
    public Guid? OriginChannelId { get; init; }

    [JsonPropertyName("partner_id")]
    public Guid? PartnerId { get; init; }

    [JsonPropertyName("stage_id")]
    public Guid? StageId { get; init; }

    [JsonPropertyName("stage_category")]
    public string? StageCategory { get; init; }

    [JsonPropertyName("created_from")]
    public DateOnly? CreatedFrom { get; init; }

    [JsonPropertyName("created_to")]
    public DateOnly? CreatedTo { get; init; }

    [JsonPropertyName("is_stale")]
    public bool? IsStale { get; init; }

    [JsonPropertyName("search_text")]
    public string? SearchText { get; init; }
}
