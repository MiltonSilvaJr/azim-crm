using Digest.Application.Models;
using Digest.Application.Tests.Stubs;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Xunit;

namespace Digest.Application.Tests.Ports;

/// <summary>
/// Testes de contrato das 6 interfaces de porta de Application (TASK-09).
/// Verifica que stubs tratam ausência de dados sem lançar exceção (Req 6.4, design §6.4).
/// </summary>
public sealed class ReadPortContractTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // ------------------------------------------------------------------
    // IActivityReadPort
    // ------------------------------------------------------------------

    [Fact(DisplayName = "IActivityReadPort retorna lista vazia quando não há atividades — sem exceção")]
    public async Task ActivityPort_ReturnsEmptyList_WhenNoData()
    {
        var port = new InMemoryActivityReadPort();

        var overdue = await port.GetOverdueActivitiesAsync(TenantId, UserId, Today);
        var today = await port.GetTodayActivitiesAsync(TenantId, UserId, Today);

        Assert.Empty(overdue);
        Assert.Empty(today);
    }

    [Fact(DisplayName = "IActivityReadPort filtra por ownerId corretamente")]
    public async Task ActivityPort_FiltersByOwner()
    {
        var port = new InMemoryActivityReadPort();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        port.SetOverdue(new[]
        {
            new ActivityItem(Guid.NewGuid(), ownerId, Today.AddDays(-1), "Tarefa A"),
            new ActivityItem(Guid.NewGuid(), otherId, Today.AddDays(-2), "Tarefa B"),
        });

        var result = await port.GetOverdueActivitiesAsync(TenantId, ownerId, Today);

        Assert.Single(result);
        Assert.Equal(ownerId, result[0].OwnerId);
    }

    // ------------------------------------------------------------------
    // IOpportunityReadPort
    // ------------------------------------------------------------------

    [Fact(DisplayName = "IOpportunityReadPort retorna listas vazias quando não há oportunidades — sem exceção")]
    public async Task OpportunityPort_ReturnsEmptyLists_WhenNoData()
    {
        var port = new InMemoryOpportunityReadPort();

        var stale = await port.GetStaleOpportunitiesAsync(TenantId, UserId);
        var closings = await port.GetOverdueClosingsAsync(TenantId, UserId, Today);
        var pipeline = await port.GetWeightedPipelineAsync(TenantId);

        Assert.Empty(stale);
        Assert.Empty(closings);
        Assert.Empty(pipeline);
    }

    // ------------------------------------------------------------------
    // IForecastReadPort — retorna null quando não há meta (Req 5.4, PBT-04)
    // ------------------------------------------------------------------

    [Fact(DisplayName = "IForecastReadPort retorna null quando não há meta cadastrada — sem exceção")]
    public async Task ForecastPort_ReturnsNull_WhenNoForecast()
    {
        var port = new InMemoryForecastReadPort();
        port.SetBlock(null);

        var result = await port.GetForecastBlockAsync(TenantId, Today);

        Assert.Null(result);
    }

    [Fact(DisplayName = "IForecastReadPort retorna bloco de metas quando há meta configurada")]
    public async Task ForecastPort_ReturnsBlock_WhenForecastExists()
    {
        var port = new InMemoryForecastReadPort();
        var block = new ForecastBlock(
            new MoneyCents(100_000_00),
            new MoneyCents(5_000_00),
            new MoneyCents(50_000_00),
            new MoneyCents(80_000_00),
            WonCount: 3,
            LostCount: 1);
        port.SetBlock(block);

        var result = await port.GetForecastBlockAsync(TenantId, Today);

        Assert.NotNull(result);
        Assert.Equal(100_000_00L, result.PipelineWeighted.Cents);
    }

    // ------------------------------------------------------------------
    // IUserDirectoryPort
    // ------------------------------------------------------------------

    [Fact(DisplayName = "IUserDirectoryPort retorna lista de tenants configurados")]
    public async Task UserDirectoryPort_ReturnsTenants()
    {
        var port = new InMemoryUserDirectoryPort();
        port.AddTenant(new TenantInfo(TenantId, "America/Sao_Paulo", new TimeOnly(7, 0), Active: true));

        var tenants = await port.GetActiveTenantInfosAsync();

        Assert.Single(tenants);
        Assert.Equal(TenantId, tenants[0].TenantId);
    }

    [Fact(DisplayName = "IUserDirectoryPort retorna lista vazia de usuários quando não há usuários no tenant")]
    public async Task UserDirectoryPort_ReturnsEmptyUsers_WhenNoUsers()
    {
        var port = new InMemoryUserDirectoryPort();

        var users = await port.GetActiveUsersAsync(TenantId);

        Assert.Empty(users);
    }

    [Fact(DisplayName = "IUserDirectoryPort isola usuários por tenant")]
    public async Task UserDirectoryPort_IsolatesUsersByTenant()
    {
        var port = new InMemoryUserDirectoryPort();
        var otherTenantId = Guid.NewGuid();
        port.AddUser(new UserInfo(UserId, TenantId, RecipientPapel.Vendedor, Active: true));
        port.AddUser(new UserInfo(Guid.NewGuid(), otherTenantId, RecipientPapel.TAdmin, Active: true));

        var users = await port.GetActiveUsersAsync(TenantId);

        Assert.Single(users);
        Assert.Equal(UserId, users[0].UserId);
    }

    // ------------------------------------------------------------------
    // IUserDigestPreferencePort
    // ------------------------------------------------------------------

    [Fact(DisplayName = "IUserDigestPreferencePort retorna OptOut=false por padrão quando não há registro")]
    public async Task PreferencePort_ReturnsOptOutFalse_WhenNoRecord()
    {
        var port = new InMemoryUserDigestPreferencePort();

        var pref = await port.GetPreferenceAsync(TenantId, UserId);

        Assert.False(pref.OptOut);
        Assert.Equal(UserId, pref.UserId);
    }

    [Fact(DisplayName = "IUserDigestPreferencePort retorna OptOut=true quando configurado")]
    public async Task PreferencePort_ReturnsOptOut_WhenSet()
    {
        var port = new InMemoryUserDigestPreferencePort();
        port.SetOptOut(UserId, optOut: true);

        var pref = await port.GetPreferenceAsync(TenantId, UserId);

        Assert.True(pref.OptOut);
    }

    // ------------------------------------------------------------------
    // IActionTokenFactory
    // ------------------------------------------------------------------

    [Fact(DisplayName = "IActionTokenFactory emite token válido com hash não-nulo")]
    public async Task ActionTokenFactory_EmitsValidToken()
    {
        var factory = new InMemoryActionTokenFactory();

        var token = await factory.IssueAsync(TenantId, UserId, Guid.NewGuid(), ActionType.Complete);

        Assert.NotNull(token);
        Assert.NotEmpty(token.ClearToken);
        Assert.NotEmpty(token.TokenHash);
        Assert.Equal(32, token.TokenHash.Length); // SHA-256 = 32 bytes
    }
}
