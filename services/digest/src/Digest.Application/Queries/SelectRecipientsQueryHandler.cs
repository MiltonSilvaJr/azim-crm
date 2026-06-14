using Digest.Application.Models;
using Digest.Application.Ports;
using Digest.Domain.Policies;
using MediatR;
using NodaTime;

namespace Digest.Application.Queries;

/// <summary>
/// Handler de <see cref="SelectRecipientsQuery"/>.
/// Obtém usuários ativos via <see cref="IUserDirectoryPort"/>, pendências via portas de leitura,
/// opt-out via <see cref="IUserDigestPreferencePort"/>, e aplica <see cref="RecipientSelectionPolicy"/>
/// a cada candidato (design §5.2, Req 3, PBT-03).
/// </summary>
public sealed class SelectRecipientsQueryHandler
    : IRequestHandler<SelectRecipientsQuery, IReadOnlyList<RecipientCandidate>>
{
    private readonly IUserDirectoryPort _userDirectory;
    private readonly IActivityReadPort _activityPort;
    private readonly IOpportunityReadPort _opportunityPort;
    private readonly IUserDigestPreferencePort _preferencePort;

    /// <summary>
    /// Constrói o handler com as dependências necessárias.
    /// </summary>
    public SelectRecipientsQueryHandler(
        IUserDirectoryPort userDirectory,
        IActivityReadPort activityPort,
        IOpportunityReadPort opportunityPort,
        IUserDigestPreferencePort preferencePort)
    {
        _userDirectory = userDirectory;
        _activityPort = activityPort;
        _opportunityPort = opportunityPort;
        _preferencePort = preferencePort;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecipientCandidate>> Handle(
        SelectRecipientsQuery request,
        CancellationToken cancellationToken)
    {
        var users = await _userDirectory.GetActiveUsersAsync(request.TenantId, cancellationToken);
        var preferences = await _preferencePort.GetAllPreferencesAsync(request.TenantId, cancellationToken);

        var prefLookup = preferences.ToDictionary(p => p.UserId);
        var referenceDate = DateOnly.FromDateTime(request.DigestDate.Value.ToDateTimeUnspecified());
        var weekday = request.DigestDate.Value.DayOfWeek; // IsoDayOfWeek

        var candidates = new List<RecipientCandidate>();

        foreach (var user in users)
        {
            if (!user.Active)
                continue;

            var optOut = prefLookup.TryGetValue(user.UserId, out var pref) && pref.OptOut;

            // Verifica pendências: atividades vencidas ou do dia + oportunidades estagnadas/fechamentos vencidos
            var overdueActivities = await _activityPort.GetOverdueActivitiesAsync(
                request.TenantId, user.UserId, referenceDate, cancellationToken);
            var todayActivities = await _activityPort.GetTodayActivitiesAsync(
                request.TenantId, user.UserId, referenceDate, cancellationToken);
            var staleOpportunities = await _opportunityPort.GetStaleOpportunitiesAsync(
                request.TenantId, user.UserId, cancellationToken);
            var overdueClosings = await _opportunityPort.GetOverdueClosingsAsync(
                request.TenantId, user.UserId, referenceDate, cancellationToken);

            var hasOwnPendencias = overdueActivities.Count > 0
                || todayActivities.Count > 0
                || staleOpportunities.Count > 0
                || overdueClosings.Count > 0;

            var result = RecipientSelectionPolicy.Evaluate(
                user.Papel,
                hasOwnPendencias,
                optOut,
                weekday,
                user.Active);

            if (result.ShouldReceiveAny)
                candidates.Add(new RecipientCandidate(user.UserId, request.TenantId, user.Papel, result));
        }

        return candidates.AsReadOnly();
    }
}
