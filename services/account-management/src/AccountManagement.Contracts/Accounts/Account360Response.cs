using AccountManagement.Contracts.Contacts;

namespace AccountManagement.Contracts.Accounts;

/// <summary>
/// Resposta da visão 360° de uma conta.
///
/// Combina dados próprios (conta + contatos) com dados externos de oportunidades e atividades.
/// Seções externas podem estar indisponíveis por degradação parcial — sinalizado via
/// <see cref="OpportunitiesUnavailable"/> e <see cref="ActivitiesUnavailable"/> (design §15, DD-004).
///
/// Oportunidades retornadas são restritas ao escopo de BUs do usuário (Req 6.2/6.3, PBT-05).
/// Histórico em ordem cronológica decrescente (Req 6.4).
///
/// Mapeia: design §8, Req 6, PBT-05, DD-004.
/// </summary>
public sealed record Account360Response(
    /// <summary>Dados da conta central.</summary>
    AccountResponse Account,
    /// <summary>Contatos ativos (PII somente após autorização RBAC).</summary>
    IReadOnlyList<ContactResponse> Contacts,
    /// <summary>
    /// Oportunidades no escopo de BUs do usuário.
    /// <c>null</c> quando a porta de oportunidades estiver indisponível (<see cref="OpportunitiesUnavailable"/>).
    /// </summary>
    IReadOnlyList<OpportunityItemResponse>? Opportunities,
    /// <summary>
    /// Atividades da conta.
    /// <c>null</c> quando a porta de atividades estiver indisponível (<see cref="ActivitiesUnavailable"/>).
    /// </summary>
    IReadOnlyList<ActivityItemResponse>? Activities,
    /// <summary>
    /// <c>true</c> quando a seção de oportunidades não pôde ser carregada (degradação parcial).
    /// O restante da 360° permanece disponível.
    /// </summary>
    bool OpportunitiesUnavailable,
    /// <summary>
    /// <c>true</c> quando a seção de atividades não pôde ser carregada (degradação parcial).
    /// O restante da 360° permanece disponível.
    /// </summary>
    bool ActivitiesUnavailable);

/// <summary>
/// Item de oportunidade na visão 360° (sem PII).
///
/// Mapeia: design §8, Req 6.1, PBT-05.
/// </summary>
public sealed record OpportunityItemResponse(
    /// <summary>Identificador da oportunidade.</summary>
    Guid OpportunityId,
    /// <summary>BU proprietária da oportunidade (dentro do escopo autorizado).</summary>
    Guid BuId,
    /// <summary>Título da oportunidade.</summary>
    string Title,
    /// <summary>Estágio atual da oportunidade.</summary>
    string Stage,
    /// <summary>Valor estimado em centavos (minor units).</summary>
    long Value);

/// <summary>
/// Item de atividade na visão 360°.
///
/// Mapeia: design §8, Req 6.1.
/// </summary>
public sealed record ActivityItemResponse(
    /// <summary>Identificador da atividade.</summary>
    Guid ActivityId,
    /// <summary>Tipo da atividade (ex.: "reunião", "ligação").</summary>
    string ActivityType,
    /// <summary>Descrição resumida da atividade.</summary>
    string Summary,
    /// <summary>Data/hora da atividade (UTC — histograma cronológico decrescente — Req 6.4).</summary>
    DateTimeOffset OccurredAt);
