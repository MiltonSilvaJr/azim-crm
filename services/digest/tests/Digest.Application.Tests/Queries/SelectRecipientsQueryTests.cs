using Digest.Application.Models;
using Digest.Application.Queries;
using Digest.Application.Tests.Stubs;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using NodaTime;
using Xunit;

namespace Digest.Application.Tests.Queries;

/// <summary>
/// Testes unitários de <see cref="SelectRecipientsQueryHandler"/> (TASK-10).
/// Verifica a seleção de destinatários por papel, pendências e opt-out (Req 3, design §5.2).
/// </summary>
public sealed class SelectRecipientsQueryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // Data local: segunda-feira 2026-06-08
    private static readonly DigestDate MondayDate = new(new LocalDate(2026, 6, 8));
    // Data local: terça-feira 2026-06-09
    private static readonly DigestDate TuesdayDate = new(new LocalDate(2026, 6, 9));

    private static readonly DateOnly Today = new(2026, 6, 8);

    private SelectRecipientsQueryHandler BuildHandler(
        InMemoryUserDirectoryPort userDir,
        InMemoryActivityReadPort? activityPort = null,
        InMemoryOpportunityReadPort? opportunityPort = null,
        InMemoryUserDigestPreferencePort? prefPort = null)
    {
        return new SelectRecipientsQueryHandler(
            userDir,
            activityPort ?? new InMemoryActivityReadPort(),
            opportunityPort ?? new InMemoryOpportunityReadPort(),
            prefPort ?? new InMemoryUserDigestPreferencePort());
    }

    [Fact(DisplayName = "Usuário inativo nunca aparece como RecipientCandidate")]
    public async Task InactiveUser_NeverCandidate()
    {
        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(Guid.NewGuid(), TenantId, RecipientPapel.Vendedor, Active: false));

        var handler = BuildHandler(userDir);
        var result = await handler.Handle(new SelectRecipientsQuery(TenantId, MondayDate), default);

        Assert.Empty(result);
    }

    [Fact(DisplayName = "Viewer nunca aparece como RecipientCandidate mesmo com pendências (Req 3.5, 3.7)")]
    public async Task Viewer_NeverCandidate_EvenWithPendencias()
    {
        var userId = Guid.NewGuid();
        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(userId, TenantId, RecipientPapel.Viewer, Active: true));

        var activityPort = new InMemoryActivityReadPort();
        activityPort.SetOverdue(new[]
        {
            new ActivityItem(Guid.NewGuid(), userId, new DateOnly(2026, 6, 7), "Tarefa"),
        });

        var handler = BuildHandler(userDir, activityPort);
        var result = await handler.Handle(new SelectRecipientsQuery(TenantId, MondayDate), default);

        Assert.Empty(result);
    }

    [Fact(DisplayName = "Vendedor com pendências próprias e sem opt-out é selecionado (Req 3.1)")]
    public async Task Vendedor_WithPendencias_NoOptOut_IsSelected()
    {
        var userId = Guid.NewGuid();
        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(userId, TenantId, RecipientPapel.Vendedor, Active: true));

        var activityPort = new InMemoryActivityReadPort();
        activityPort.SetOverdue(new[]
        {
            new ActivityItem(Guid.NewGuid(), userId, new DateOnly(2026, 6, 7), "Tarefa vencida"),
        });

        var handler = BuildHandler(userDir, activityPort);
        var result = await handler.Handle(new SelectRecipientsQuery(TenantId, TuesdayDate), default);

        Assert.Single(result);
        Assert.True(result[0].SelectionResult.ShouldReceivePendencias);
    }

    [Fact(DisplayName = "Vendedor sem pendências e sem papel de gestão nunca é selecionado (Req 3.2, RN-011)")]
    public async Task Vendedor_NoPendencias_NeverSelected()
    {
        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(Guid.NewGuid(), TenantId, RecipientPapel.Vendedor, Active: true));

        var handler = BuildHandler(userDir);
        var result = await handler.Handle(new SelectRecipientsQuery(TenantId, TuesdayDate), default);

        Assert.Empty(result);
    }

    [Fact(DisplayName = "TAdmin sem pendências na segunda-feira é selecionado para azimute (Req 3.3)")]
    public async Task TAdmin_NoPendencias_Monday_SelectedForAzimute()
    {
        var userId = Guid.NewGuid();
        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(userId, TenantId, RecipientPapel.TAdmin, Active: true));

        var handler = BuildHandler(userDir);
        var result = await handler.Handle(new SelectRecipientsQuery(TenantId, MondayDate), default);

        Assert.Single(result);
        Assert.True(result[0].SelectionResult.ShouldReceiveAzimute);
        Assert.False(result[0].SelectionResult.ShouldReceivePendencias);
    }

    [Fact(DisplayName = "GestorBU com opt-out na segunda-feira recebe azimute mas não pendências (Req 10.2)")]
    public async Task GestorBU_OptOut_Monday_ReceivesAzimute_NotPendencias()
    {
        var userId = Guid.NewGuid();
        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(userId, TenantId, RecipientPapel.GestorBU, Active: true));

        var activityPort = new InMemoryActivityReadPort();
        activityPort.SetOverdue(new[]
        {
            new ActivityItem(Guid.NewGuid(), userId, new DateOnly(2026, 6, 7), "Tarefa"),
        });

        var prefPort = new InMemoryUserDigestPreferencePort();
        prefPort.SetOptOut(userId, optOut: true);

        var handler = BuildHandler(userDir, activityPort, prefPort: prefPort);
        var result = await handler.Handle(new SelectRecipientsQuery(TenantId, MondayDate), default);

        Assert.Single(result);
        Assert.True(result[0].SelectionResult.ShouldReceiveAzimute);
        Assert.False(result[0].SelectionResult.ShouldReceivePendencias);
    }

    [Fact(DisplayName = "TAdmin sem pendências na terça-feira não é selecionado")]
    public async Task TAdmin_NoPendencias_Tuesday_NeverSelected()
    {
        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(Guid.NewGuid(), TenantId, RecipientPapel.TAdmin, Active: true));

        var handler = BuildHandler(userDir);
        var result = await handler.Handle(new SelectRecipientsQuery(TenantId, TuesdayDate), default);

        Assert.Empty(result);
    }

    [Fact(DisplayName = "Usuário com opt-out e sem papel de gestão não recebe nada (Req 10.1)")]
    public async Task UserWithOptOut_NoGestao_ReceivesNothing()
    {
        var userId = Guid.NewGuid();
        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(userId, TenantId, RecipientPapel.Vendedor, Active: true));

        var activityPort = new InMemoryActivityReadPort();
        activityPort.SetOverdue(new[]
        {
            new ActivityItem(Guid.NewGuid(), userId, new DateOnly(2026, 6, 7), "Tarefa"),
        });

        var prefPort = new InMemoryUserDigestPreferencePort();
        prefPort.SetOptOut(userId, optOut: true);

        var handler = BuildHandler(userDir, activityPort, prefPort: prefPort);
        var result = await handler.Handle(new SelectRecipientsQuery(TenantId, TuesdayDate), default);

        Assert.Empty(result);
    }
}
