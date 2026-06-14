using System.Text.Json.Serialization;

namespace OpportunityPipeline.Contracts.Responses;

/// <summary>
/// Response completo de oportunidade.
/// Money sempre em centavos inteiros (long) — nunca decimal (RNF 11.1).
/// Mapeia: design §8, Req 1..20, TASK-19.
/// </summary>
public sealed class OpportunityResponse
{
    /// <summary>ID da oportunidade.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>Número humano imutável (ex.: "AZ-0001") — PBT-02.</summary>
    [JsonPropertyName("opportunity_number")]
    public string OpportunityNumber { get; init; } = string.Empty;

    /// <summary>Título da oportunidade (sem PII — INV-13).</summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>ID do tenant.</summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    /// <summary>ID da BU.</summary>
    [JsonPropertyName("bu_id")]
    public Guid BuId { get; init; }

    /// <summary>ID da conta.</summary>
    [JsonPropertyName("account_id")]
    public Guid AccountId { get; init; }

    /// <summary>ID do proprietário.</summary>
    [JsonPropertyName("owner_id")]
    public Guid OwnerId { get; init; }

    /// <summary>ID do parceiro (nullable — presente apenas em canal Parceiro).</summary>
    [JsonPropertyName("partner_id")]
    public Guid? PartnerId { get; init; }

    /// <summary>ID do estágio atual.</summary>
    [JsonPropertyName("stage_id")]
    public Guid StageId { get; init; }

    /// <summary>Nome do estágio atual.</summary>
    [JsonPropertyName("stage_name")]
    public string StageName { get; init; } = string.Empty;

    /// <summary>Categoria do estágio: open, won, lost.</summary>
    [JsonPropertyName("stage_category")]
    public string StageCategory { get; init; } = string.Empty;

    /// <summary>ID do canal de origem.</summary>
    [JsonPropertyName("origin_channel_id")]
    public Guid OriginChannelId { get; init; }

    /// <summary>Nome do canal de origem.</summary>
    [JsonPropertyName("origin_channel_name")]
    public string OriginChannelName { get; init; } = string.Empty;

    /// <summary>Valor de setup em centavos (long — RNF 11).</summary>
    [JsonPropertyName("valor_setup")]
    public long ValorSetup { get; init; }

    /// <summary>Valor mensal em centavos (long — RNF 11).</summary>
    [JsonPropertyName("valor_mensal")]
    public long ValorMensal { get; init; }

    /// <summary>Duração em meses.</summary>
    [JsonPropertyName("duracao_meses")]
    public int DuracaoMeses { get; init; }

    /// <summary>Valor total calculado em centavos (valor_setup + valor_mensal * duracao_meses). Nunca decimal.</summary>
    [JsonPropertyName("valor_total")]
    public long ValorTotal { get; init; }

    /// <summary>Probabilidade [0, 100].</summary>
    [JsonPropertyName("probabilidade")]
    public int Probabilidade { get; init; }

    /// <summary>Forecast ponderado em centavos (valor_total * probabilidade / 100). Nunca decimal.</summary>
    [JsonPropertyName("forecast_ponderado")]
    public long ForecastPonderado { get; init; }

    /// <summary>Forecast líquido em centavos (forecast_ponderado - comissão ponderada). Null quando sem comissão ou não calculado.</summary>
    [JsonPropertyName("forecast_liquido")]
    public long? ForecastLiquido { get; init; }

    /// <summary>Data de fechamento esperada.</summary>
    [JsonPropertyName("expected_close_date")]
    public DateOnly? ExpectedCloseDate { get; init; }

    /// <summary>ID do motivo de perda (nullable).</summary>
    [JsonPropertyName("loss_reason_id")]
    public Guid? LossReasonId { get; init; }

    /// <summary>Timestamp de encerramento (nullable).</summary>
    [JsonPropertyName("closed_at")]
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Notas livres (sem PII — INV-13).</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>Indica oportunidade estagnada (≥ 14 dias sem atividade — Req 17).</summary>
    [JsonPropertyName("is_stale")]
    public bool IsStale { get; init; }

    /// <summary>Indica oportunidade vencida (expected_close_date no passado e stage open — Req 9.3).</summary>
    [JsonPropertyName("is_overdue")]
    public bool IsOverdue { get; init; }

    /// <summary>Timestamp de criação.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Timestamp de atualização.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Response de listagem paginada de oportunidades.
/// Mapeia: design §8, Req 19, TASK-19.
/// </summary>
public sealed class OpportunityListResponse
{
    /// <summary>Itens da página atual.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<OpportunitySummaryResponse> Items { get; init; } = [];

    /// <summary>Total de registros sem paginação.</summary>
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    /// <summary>Página atual (1-based).</summary>
    [JsonPropertyName("page")]
    public int Page { get; init; }

    /// <summary>Tamanho da página (máx 200).</summary>
    [JsonPropertyName("page_size")]
    public int PageSize { get; init; }
}

/// <summary>
/// Resumo de oportunidade para listagem.
/// Money em centavos inteiros (long — RNF 11.1).
/// </summary>
public sealed class OpportunitySummaryResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("opportunity_number")]
    public string OpportunityNumber { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("owner_id")]
    public Guid OwnerId { get; init; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; init; }

    [JsonPropertyName("partner_id")]
    public Guid? PartnerId { get; init; }

    [JsonPropertyName("stage_name")]
    public string StageName { get; init; } = string.Empty;

    [JsonPropertyName("stage_category")]
    public string StageCategory { get; init; } = string.Empty;

    /// <summary>Valor total em centavos (long). Nunca decimal.</summary>
    [JsonPropertyName("valor_total")]
    public long ValorTotal { get; init; }

    /// <summary>Forecast ponderado em centavos (long). Nunca decimal.</summary>
    [JsonPropertyName("forecast_ponderado")]
    public long ForecastPonderado { get; init; }

    [JsonPropertyName("is_stale")]
    public bool IsStale { get; init; }

    [JsonPropertyName("is_overdue")]
    public bool IsOverdue { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Response do Kanban com colunas por estágio.
/// Inclui somas agregadas (valor_total e forecast_ponderado) por coluna.
/// Mapeia: design §8, Req 18, RNF 1, TASK-19.
/// </summary>
public sealed class KanbanResponse
{
    /// <summary>ID da BU do kanban.</summary>
    [JsonPropertyName("bu_id")]
    public Guid BuId { get; init; }

    /// <summary>Colunas do kanban (uma por estágio).</summary>
    [JsonPropertyName("columns")]
    public IReadOnlyList<KanbanColumnResponse> Columns { get; init; } = [];
}

/// <summary>
/// Coluna do kanban com somas e cards paginados.
/// total_valor e total_forecast em centavos (long — RNF 11.1).
/// </summary>
public sealed class KanbanColumnResponse
{
    [JsonPropertyName("stage_id")]
    public Guid StageId { get; init; }

    [JsonPropertyName("stage_name")]
    public string StageName { get; init; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; init; }

    /// <summary>Soma do valor_total de todas as oportunidades na coluna, em centavos.</summary>
    [JsonPropertyName("total_valor")]
    public long TotalValor { get; init; }

    /// <summary>Soma do forecast_ponderado de todas as oportunidades na coluna, em centavos.</summary>
    [JsonPropertyName("total_forecast")]
    public long TotalForecast { get; init; }

    /// <summary>Total de oportunidades na coluna (sem paginação).</summary>
    [JsonPropertyName("total_count")]
    public int TotalCount { get; init; }

    /// <summary>Cards da página atual da coluna (paginação por coluna).</summary>
    [JsonPropertyName("cards")]
    public IReadOnlyList<OpportunitySummaryResponse> Cards { get; init; } = [];

    /// <summary>Indica se há mais cards além da página atual.</summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}

/// <summary>
/// Response da linha do tempo de transições de estágio.
/// Mapeia: design §8, Req 5/19, TASK-19.
/// </summary>
public sealed class TimelineResponse
{
    [JsonPropertyName("opportunity_id")]
    public Guid OpportunityId { get; init; }

    [JsonPropertyName("entries")]
    public IReadOnlyList<TimelineEntryResponse> Entries { get; init; } = [];
}

/// <summary>Entrada da linha do tempo de transição.</summary>
public sealed class TimelineEntryResponse
{
    [JsonPropertyName("transition_id")]
    public Guid TransitionId { get; init; }

    [JsonPropertyName("from_stage_name")]
    public string? FromStageName { get; init; }

    [JsonPropertyName("to_stage_name")]
    public string ToStageName { get; init; } = string.Empty;

    [JsonPropertyName("from_category")]
    public string? FromCategory { get; init; }

    [JsonPropertyName("to_category")]
    public string ToCategory { get; init; } = string.Empty;

    [JsonPropertyName("occurred_at")]
    public DateTimeOffset OccurredAt { get; init; }

    [JsonPropertyName("actor_id")]
    public Guid ActorId { get; init; }
}

/// <summary>
/// Response de comissão (projetada e/ou snapshot).
/// Money em centavos (long — RNF 11.1).
/// Mapeia: design §8, Req 11..13, TASK-19.
/// </summary>
public sealed class CommissionResponse
{
    [JsonPropertyName("opportunity_id")]
    public Guid OpportunityId { get; init; }

    [JsonPropertyName("commissions")]
    public IReadOnlyList<CommissionDetailResponse> Commissions { get; init; } = [];

    /// <summary>Forecast líquido em centavos (forecast_ponderado - comissão ponderada). Nunca decimal.</summary>
    [JsonPropertyName("forecast_liquido")]
    public long ForecastLiquido { get; init; }
}

/// <summary>Detalhe de comissão de parceiro.</summary>
public sealed class CommissionDetailResponse
{
    [JsonPropertyName("partner_id")]
    public Guid PartnerId { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("pct_setup")]
    public decimal PctSetup { get; init; }

    [JsonPropertyName("pct_recorrente")]
    public decimal PctRecorrente { get; init; }

    /// <summary>Valor fixo em centavos (long). Nunca decimal.</summary>
    [JsonPropertyName("valor_fixo")]
    public long ValorFixo { get; init; }

    [JsonPropertyName("meses_comissionados")]
    public int MesesComissionados { get; init; }

    /// <summary>Comissão total calculada em centavos (long). Nunca decimal.</summary>
    [JsonPropertyName("comissao_total")]
    public long ComissaoTotal { get; init; }

    /// <summary>Indica se é snapshot imutável (criado ao ganhar a oportunidade).</summary>
    [JsonPropertyName("is_snapshot")]
    public bool IsSnapshot { get; init; }

    [JsonPropertyName("snapshot_at")]
    public DateTimeOffset? SnapshotAt { get; init; }
}

/// <summary>
/// Response de filtro salvo.
/// Mapeia: design §8, Req 19, TASK-19.
/// </summary>
public sealed class SavedFilterResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("criteria")]
    public object Criteria { get; init; } = new();

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Response para operações de criação de oportunidade (HTTP 201).
/// Mapeia: design §8, Req 1, TASK-19.
/// </summary>
public sealed class CreateOpportunityResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("opportunity_number")]
    public string OpportunityNumber { get; init; } = string.Empty;

    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }
}

/// <summary>
/// Response do scan de estagnação (POST /internal/stale-scan → 202 Accepted).
/// Mapeia: design §8, Req 17, TASK-19.
/// </summary>
public sealed class StaleScanResponse
{
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    [JsonPropertyName("bu_id")]
    public Guid BuId { get; init; }

    [JsonPropertyName("detection_period")]
    public string DetectionPeriod { get; init; } = string.Empty;

    [JsonPropertyName("marked_stale_count")]
    public int MarkedStaleCount { get; init; }

    [JsonPropertyName("executed_at")]
    public DateTimeOffset ExecutedAt { get; init; }
}
