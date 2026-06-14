namespace GoalForecast.Contracts;

/// <summary>
/// DTO de saída de uma meta. Usado em POST /goals, PUT /goals/{id} e GET /goals.
/// Todos os valores monetários em centavos inteiros (RNF 4).
/// TenantId não incluído — proteção de over-posting e privacidade (design §10, Req 12.1).
///
/// Mapeia: design §8.1, §8.2, §8.3, TASK-21.
/// </summary>
public sealed record GoalDto(
    /// <summary>Identificador único da meta.</summary>
    Guid Id,

    /// <summary>Escopo: "BU" ou "RESPONSAVEL".</summary>
    string Scope,

    /// <summary>Identificador da BU.</summary>
    Guid BuId,

    /// <summary>Identificador do responsável; nulo no escopo BU.</summary>
    Guid? OwnerId,

    /// <summary>Ano do período.</summary>
    int Year,

    /// <summary>Mês do período (1..12).</summary>
    int Month,

    /// <summary>Valor da meta em centavos inteiros (RNF 4).</summary>
    long ValorMeta,

    /// <summary>Timestamp de criação.</summary>
    DateTimeOffset CreatedAt,

    /// <summary>Timestamp da última atualização.</summary>
    DateTimeOffset UpdatedAt);
