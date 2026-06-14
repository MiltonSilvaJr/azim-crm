using GoalForecast.Application.Commands;
using GoalForecast.Application.Common;
using GoalForecast.Application.Ports;
using GoalForecast.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using AppGoalDto = GoalForecast.Application.Common.GoalDto;

namespace GoalForecast.Api.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory para testes de integração de API do módulo goal-forecast.
///
/// Substitui:
/// - Autenticação por <see cref="FakeAuthHandler"/> (claims via header X-Test-Claims).
/// - Handlers MediatR por substitutos <c>NSubstitute</c> para isolar os testes da infrastructure.
/// - Portas de infraestrutura por stubs.
///
/// Mapeia: TASK-22..25, design §13.
/// </summary>
// WebApplicationFactory usa o tipo Program (partial) para localizar o assembly e arrancar a aplicação
public sealed class GoalForecastWebFactory : WebApplicationFactory<GoalForecast.Api.Program>
{
    // Substitutos injetáveis nos testes
    public IRequestHandler<CreateOrUpdateGoalCommand, CreateOrUpdateGoalResult> CommandHandler { get; } =
        Substitute.For<IRequestHandler<CreateOrUpdateGoalCommand, CreateOrUpdateGoalResult>>();

    public IRequestHandler<ListGoalsQuery, PagedResult<GoalDto>> ListGoalsHandler { get; } =
        Substitute.For<IRequestHandler<ListGoalsQuery, PagedResult<GoalDto>>>();

    public IRequestHandler<GetForecastPanelQuery, ForecastPanelResult> ForecastPanelHandler { get; } =
        Substitute.For<IRequestHandler<GetForecastPanelQuery, ForecastPanelResult>>();

    public IRequestHandler<GetGoalAggregateQuery, GoalAggregateResult> AggregateHandler { get; } =
        Substitute.For<IRequestHandler<GetGoalAggregateQuery, GoalAggregateResult>>();

    public IRequestHandler<GetGoalDigestBlockQuery, DigestBlockResult> DigestBlockHandler { get; } =
        Substitute.For<IRequestHandler<GetGoalDigestBlockQuery, DigestBlockResult>>();

    public IRequestHandler<UpdateGoalByIdCommand, AppGoalDto> UpdateGoalByIdHandler { get; } =
        Substitute.For<IRequestHandler<UpdateGoalByIdCommand, AppGoalDto>>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Substituir autenticação por handler de teste
            services.AddAuthentication(FakeAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>(
                    FakeAuthHandler.SchemeName, _ => { });

            // Registrar handlers substitutos (NSubstitute)
            services.RemoveAll<IRequestHandler<CreateOrUpdateGoalCommand, CreateOrUpdateGoalResult>>();
            services.AddSingleton(CommandHandler);

            services.RemoveAll<IRequestHandler<ListGoalsQuery, PagedResult<GoalDto>>>();
            services.AddSingleton(ListGoalsHandler);

            services.RemoveAll<IRequestHandler<GetForecastPanelQuery, ForecastPanelResult>>();
            services.AddSingleton(ForecastPanelHandler);

            services.RemoveAll<IRequestHandler<GetGoalAggregateQuery, GoalAggregateResult>>();
            services.AddSingleton(AggregateHandler);

            services.RemoveAll<IRequestHandler<GetGoalDigestBlockQuery, DigestBlockResult>>();
            services.AddSingleton(DigestBlockHandler);

            services.RemoveAll<IRequestHandler<UpdateGoalByIdCommand, AppGoalDto>>();
            services.AddSingleton(UpdateGoalByIdHandler);
        });
    }
}
