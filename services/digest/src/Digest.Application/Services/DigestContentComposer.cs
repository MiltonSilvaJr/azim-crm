using Digest.Application.Models;
using Digest.Application.Ports;
using Digest.Domain.Aggregates;
using Digest.Domain.ValueObjects;

namespace Digest.Application.Services;

/// <summary>
/// Serviço de Application que monta os blocos canônicos de conteúdo do digest para um usuário.
/// Orquestra as portas de leitura e delega à <see cref="AzimuteSectionBuilder"/> na segunda-feira
/// para usuários de gestão (design §5.3, Req 4, Req 5, PBT-06).
/// </summary>
/// <remarks>
/// Blocos compostos:
/// <list type="bullet">
///   <item><c>overdue_activities</c> — atividades vencidas do usuário.</item>
///   <item><c>today_activities</c> — atividades previstas para hoje.</item>
///   <item><c>stale_opportunities</c> — oportunidades estagnadas.</item>
///   <item><c>overdue_closings</c> — fechamentos de oportunidades vencidos.</item>
///   <item><c>azimute_pipeline</c> + <c>azimute_metas</c> — apenas segunda-feira e papel gestão (PBT-06).</item>
/// </list>
/// Todos os valores monetários em <see cref="MoneyCents"/> (DD-010).
/// </remarks>
public sealed class DigestContentComposer
{
    /// <summary>Chave do bloco de atividades vencidas.</summary>
    public const string OverdueActivitiesKey = "overdue_activities";

    /// <summary>Chave do bloco de atividades do dia.</summary>
    public const string TodayActivitiesKey = "today_activities";

    /// <summary>Chave do bloco de oportunidades estagnadas.</summary>
    public const string StaleOpportunitiesKey = "stale_opportunities";

    /// <summary>Chave do bloco de fechamentos vencidos.</summary>
    public const string OverdueClosingsKey = "overdue_closings";

    private readonly IActivityReadPort _activityPort;
    private readonly IOpportunityReadPort _opportunityPort;
    private readonly AzimuteSectionBuilder _azimuteSectionBuilder;

    /// <summary>
    /// Constrói o composer com as dependências necessárias.
    /// </summary>
    public DigestContentComposer(
        IActivityReadPort activityPort,
        IOpportunityReadPort opportunityPort,
        AzimuteSectionBuilder azimuteSectionBuilder)
    {
        _activityPort = activityPort;
        _opportunityPort = opportunityPort;
        _azimuteSectionBuilder = azimuteSectionBuilder;
    }

    /// <summary>
    /// Compõe o <see cref="DigestContent"/> para o candidato especificado.
    /// PBT-06: azimute presente se, e somente se, dia local = segunda e papel é de gestão.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="candidate">Candidato a destinatário com resultado da policy de seleção.</param>
    /// <param name="digestDate">Data local do digest no fuso do tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns><see cref="DigestContent"/> com os blocos compostos.</returns>
    public async Task<DigestContent> ComposeAsync(
        Guid tenantId,
        RecipientCandidate candidate,
        DigestDate digestDate,
        CancellationToken cancellationToken = default)
    {
        var referenceDate = DateOnly.FromDateTime(digestDate.Value.ToDateTimeUnspecified());
        var sections = new List<DigestSection>();

        // --- Blocos de pendências (quando o candidato deve receber — SelectionResult.ShouldReceivePendencias) ---
        if (candidate.SelectionResult.ShouldReceivePendencias)
        {
            var overdueActs = await _activityPort.GetOverdueActivitiesAsync(
                tenantId, candidate.UserId, referenceDate, cancellationToken);
            sections.Add(new DigestSection(OverdueActivitiesKey,
                overdueActs.Select(a => a.Title).ToList()));

            var todayActs = await _activityPort.GetTodayActivitiesAsync(
                tenantId, candidate.UserId, referenceDate, cancellationToken);
            sections.Add(new DigestSection(TodayActivitiesKey,
                todayActs.Select(a => a.Title).ToList()));

            var staleOpps = await _opportunityPort.GetStaleOpportunitiesAsync(
                tenantId, candidate.UserId, cancellationToken);
            sections.Add(new DigestSection(StaleOpportunitiesKey,
                staleOpps.Select(o => o.Name).ToList()));

            var overdueClosings = await _opportunityPort.GetOverdueClosingsAsync(
                tenantId, candidate.UserId, referenceDate, cancellationToken);
            sections.Add(new DigestSection(OverdueClosingsKey,
                overdueClosings.Select(o => o.Name).ToList()));
        }

        // --- Bloco de azimute: SOMENTE segunda-feira e papel gestão (PBT-06, Req 5.7, RN-029) ---
        if (candidate.SelectionResult.ShouldReceiveAzimute)
        {
            var azimuteSections = await _azimuteSectionBuilder.BuildAsync(
                tenantId, referenceDate, cancellationToken);
            sections.AddRange(azimuteSections);
        }

        return new DigestContent(sections);
    }

    /// <summary>
    /// Determina se o usuário deve receber o digest com base no conteúdo composto.
    /// Retorna <see langword="false"/> quando não há bloco e o usuário não é gestor em segunda (Req 4.6).
    /// </summary>
    public static bool ShouldSend(DigestContent content, RecipientCandidate candidate)
    {
        return content.HasContent();
    }
}
