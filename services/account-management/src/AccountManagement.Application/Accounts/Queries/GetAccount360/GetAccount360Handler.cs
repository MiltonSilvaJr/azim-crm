using AccountManagement.Application.Exceptions;
using AccountManagement.Domain.Accounts.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccountManagement.Application.Accounts.Queries.GetAccount360;

/// <summary>
/// Handler para <see cref="GetAccount360Query"/>.
///
/// Compõe a visão 360° de forma síncrona:
/// 1. Carrega o agregado <see cref="Domain.Accounts.Account"/> (dados próprios + contatos).
/// 2. Chama <see cref="IOpportunityReadPort"/> passando o escopo de BUs do usuário (PBT-05).
/// 3. Chama <see cref="IActivityReadPort"/>.
/// 4. Aplica degradação parcial: se uma porta falhar por exception (timeout, indisponibilidade),
///    a seção correspondente é marcada como indisponível em <see cref="SectionAvailability"/>
///    sem lançar exceção — a 360° não derruba (design §15, DD-004).
///
/// A filtragem por BU é de responsabilidade da porta de leitura; o handler apenas
/// repassa <see cref="GetAccount360Query.AuthorizedBuIds"/> (PBT-05, Req 6.2/6.3).
///
/// Mapeia: design §5.2, §5.3, §15, Req 6, PBT-05, DD-004, ACC-ERR-003.
/// </summary>
internal sealed class GetAccount360Handler : IRequestHandler<GetAccount360Query, Account360View>
{
    private readonly IAccountRepository _repository;
    private readonly IOpportunityReadPort _opportunityPort;
    private readonly IActivityReadPort _activityPort;
    private readonly ILogger<GetAccount360Handler> _logger;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public GetAccount360Handler(
        IAccountRepository repository,
        IOpportunityReadPort opportunityPort,
        IActivityReadPort activityPort,
        ILogger<GetAccount360Handler>? logger = null)
    {
        _repository = repository;
        _opportunityPort = opportunityPort;
        _activityPort = activityPort;
        _logger = logger ?? NullLogger<GetAccount360Handler>.Instance;
    }

    /// <inheritdoc />
    public async Task<Account360View> Handle(
        GetAccount360Query request,
        CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new AccountNotFoundException();

        // Chamadas paralelas às portas de leitura com degradação parcial independente
        var opportunitiesTask = FetchOpportunitiesSafeAsync(request, cancellationToken);
        var activitiesTask = FetchActivitiesSafeAsync(request.AccountId, cancellationToken);

        await Task.WhenAll(opportunitiesTask, activitiesTask);

        var (opportunities, opportunitiesAvailable) = await opportunitiesTask;
        var (activities, activitiesAvailable) = await activitiesTask;

        return new Account360View
        {
            Account = account,
            Contacts = account.Contacts,
            Opportunities = opportunities,
            Activities = activities,
            Availability = new SectionAvailability(
                OpportunitiesAvailable: opportunitiesAvailable,
                ActivitiesAvailable: activitiesAvailable)
        };
    }

    /// <summary>
    /// Busca oportunidades com degradação parcial.
    /// Em caso de falha, retorna <c>(null, false)</c> sem propagar a exceção.
    /// O escopo de BUs (<see cref="GetAccount360Query.AuthorizedBuIds"/>) é repassado
    /// integralmente à porta de leitura (PBT-05).
    /// </summary>
    private async Task<(IReadOnlyList<OpportunityReadModel>? Opportunities, bool Available)>
        FetchOpportunitiesSafeAsync(GetAccount360Query request, CancellationToken cancellationToken)
    {
        try
        {
            var opportunities = await _opportunityPort.GetByAccountAsync(
                request.AccountId,
                request.AuthorizedBuIds,
                cancellationToken);
            return (opportunities, true);
        }
        catch (Exception ex) when (ex is TimeoutException or TaskCanceledException or HttpRequestException or OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Porta de oportunidades indisponível para conta {AccountId}. Degradação parcial aplicada.",
                request.AccountId);
            return (null, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha inesperada ao buscar oportunidades para conta {AccountId}. Degradação parcial aplicada.",
                request.AccountId);
            return (null, false);
        }
    }

    /// <summary>
    /// Busca atividades com degradação parcial.
    /// Em caso de falha, retorna <c>(null, false)</c> sem propagar a exceção.
    /// </summary>
    private async Task<(IReadOnlyList<ActivityReadModel>? Activities, bool Available)>
        FetchActivitiesSafeAsync(Guid accountId, CancellationToken cancellationToken)
    {
        try
        {
            var activities = await _activityPort.GetByAccountAsync(accountId, cancellationToken);
            return (activities, true);
        }
        catch (Exception ex) when (ex is TimeoutException or TaskCanceledException or HttpRequestException or OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Porta de atividades indisponível para conta {AccountId}. Degradação parcial aplicada.",
                accountId);
            return (null, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha inesperada ao buscar atividades para conta {AccountId}. Degradação parcial aplicada.",
                accountId);
            return (null, false);
        }
    }
}
