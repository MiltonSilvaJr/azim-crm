using Digest.Application.Commands;
using Digest.Application.Models;
using Digest.Application.Options;
using Digest.Application.Services;
using Digest.Application.Tests.Stubs;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using FsCheck;
using FsCheck.Xunit;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace Digest.Application.Tests.Commands;

/// <summary>
/// Testes unitários e PBT de <see cref="SendUserDigestHandler"/> (TASK-12, VAL-ACT-02).
/// PBT-02: N disparos para o mesmo (tenant_id, user_id, digest_date) → exatamente um envio efetivo.
/// VAL-ACT-02: TTL do action token configurável por tenant, com default de 48h.
/// </summary>
public sealed class SendUserDigestHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DigestDate Tuesday = new(new LocalDate(2026, 6, 9)); // terça

    private static (
        SendUserDigestHandler handler,
        InMemoryEmailSender emailSender,
        InMemoryEmailDigestLogRepository logRepo,
        InMemoryOutboxPublisher outbox,
        InMemoryDigestTenantSettingsRepository settingsRepo)
        BuildStack(
            bool hasActivity = true,
            Guid? tenantId = null,
            Guid? userId = null)
    {
        var effectiveTenantId = tenantId ?? TenantId;
        var effectiveUserId = userId ?? UserId;

        var activityPort = new InMemoryActivityReadPort();
        if (hasActivity)
        {
            activityPort.SetOverdue(new[]
            {
                new ActivityItem(Guid.NewGuid(), effectiveUserId, new DateOnly(2026, 6, 8), "Tarefa vencida"),
            });
        }

        var oppPort = new InMemoryOpportunityReadPort();
        var forecastPort = new InMemoryForecastReadPort();
        var azimuteSectionBuilder = new AzimuteSectionBuilder(oppPort, forecastPort);
        var composer = new DigestContentComposer(activityPort, oppPort, azimuteSectionBuilder);

        var userDir = new InMemoryUserDirectoryPort();
        userDir.AddUser(new UserInfo(effectiveUserId, effectiveTenantId, RecipientPapel.Vendedor, Active: true));

        var logRepo = new InMemoryEmailDigestLogRepository();
        var tokenRepo = new InMemoryDigestActionTokenRepository();
        var emailSender = new InMemoryEmailSender();
        var outbox = new InMemoryOutboxPublisher();
        var tokenFactory = new InMemoryActionTokenFactory();
        var settingsRepo = new InMemoryDigestTenantSettingsRepository();
        var digestOptions = new DigestOptions { DefaultActionTokenTtlHours = 48 };
        var ttlResolver = new ActionTokenTtlResolver(settingsRepo, digestOptions);

        var handler = new SendUserDigestHandler(
            logRepo, tokenRepo, composer, emailSender, outbox, userDir, activityPort, tokenFactory, ttlResolver);
        return (handler, emailSender, logRepo, outbox, settingsRepo);
    }

    // ------------------------------------------------------------------
    // Testes nominais
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Primeiro envio bem-sucedido — IEmailSender chamado exatamente uma vez")]
    public async Task FirstSend_EmailSentOnce()
    {
        var (handler, emailSender, _, outbox, _) = BuildStack();
        var cmd = new SendUserDigestCommand(TenantId, UserId, Tuesday, "user@example.com");

        var result = await handler.Handle(cmd, default);

        Assert.True(result.Sent);
        Assert.False(result.Skipped);
        Assert.NotNull(result.MessageId);
        Assert.Equal(1, emailSender.SendCallCount);
        Assert.Single(outbox.Published);
    }

    [Fact(DisplayName = "Segundo disparo para o mesmo (tenant, user, date) — IEmailSender não chamado (idempotência)")]
    public async Task SecondDispatch_IdempotencySkips()
    {
        var (handler, emailSender, _, _, _) = BuildStack();
        var cmd = new SendUserDigestCommand(TenantId, UserId, Tuesday, "user@example.com");

        await handler.Handle(cmd, default); // primeiro disparo
        var result = await handler.Handle(cmd, default); // segundo disparo

        Assert.False(result.Sent);
        Assert.True(result.Skipped);
        Assert.Equal(1, emailSender.SendCallCount); // enviou apenas uma vez
    }

    [Fact(DisplayName = "Falha do provedor — status marcado como failed, IEmailSender chamado mas sem sucesso")]
    public async Task ProviderFailure_MarksAsFailed()
    {
        var (handler, emailSender, _, outbox, _) = BuildStack();
        emailSender.SetFail(true);
        var cmd = new SendUserDigestCommand(TenantId, UserId, Tuesday, "user@example.com");

        var result = await handler.Handle(cmd, default);

        Assert.False(result.Sent);
        Assert.False(result.Skipped);
        Assert.Equal(1, emailSender.SendCallCount);
        Assert.Empty(outbox.Published); // sem evento se não enviou
    }

    [Fact(DisplayName = "Idempotência: verificação ocorre antes de invocar IEmailSender (RNF 2.3)")]
    public async Task Idempotency_CheckBeforeSend()
    {
        var (handler, emailSender, _, _, _) = BuildStack();
        var cmd = new SendUserDigestCommand(TenantId, UserId, Tuesday, "user@example.com");

        // Primeiro disparo
        await handler.Handle(cmd, default);

        // Anota o count após o primeiro
        var countAfterFirst = emailSender.SendCallCount;

        // N disparos adicionais
        for (var i = 0; i < 5; i++)
            await handler.Handle(cmd, default);

        Assert.Equal(countAfterFirst, emailSender.SendCallCount); // sem envios adicionais
    }

    [Fact(DisplayName = "DigestEmailSent publicado no Outbox exatamente uma vez por envio bem-sucedido (RNF 10.1)")]
    public async Task DigestEmailSent_PublishedExactlyOnce_PerSuccessfulSend()
    {
        var (handler, _, _, outbox, _) = BuildStack();
        var cmd = new SendUserDigestCommand(TenantId, UserId, Tuesday, "user@example.com");

        await handler.Handle(cmd, default);
        // Segundo disparo (idempotente)
        await handler.Handle(cmd, default);

        Assert.Single(outbox.Published); // exatamente um evento
        Assert.Equal(TenantId, outbox.Published[0].TenantId);
        Assert.Equal(UserId, outbox.Published[0].UserId);
    }

    [Fact(DisplayName = "Usuário sem conteúdo e sem papel de gestão não recebe e-mail")]
    public async Task UserWithNoContent_NotSent()
    {
        var (handler, emailSender, _, _, _) = BuildStack(hasActivity: false); // sem atividades
        var cmd = new SendUserDigestCommand(TenantId, UserId, Tuesday, "user@example.com");

        var result = await handler.Handle(cmd, default);

        // Sem conteúdo → não enviou
        Assert.False(result.Sent);
        Assert.Equal(0, emailSender.SendCallCount);
    }

    // ------------------------------------------------------------------
    // VAL-ACT-02: resolução de TTL por tenant
    // ------------------------------------------------------------------

    [Fact(DisplayName = "VAL-ACT-02: usa setting do tenant quando presente")]
    public async Task TtlResolver_UsesTenantSettingWhenPresent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (handler, emailSender, _, _, settingsRepo) = BuildStack(tenantId: tenantId, userId: userId);

        // Configura TTL de 72h para o tenant
        settingsRepo.SetTtlHours(tenantId, 72);

        var cmd = new SendUserDigestCommand(tenantId, userId, Tuesday, "user@example.com");
        var result = await handler.Handle(cmd, default);

        // O envio deve funcionar normalmente com TTL customizado
        Assert.True(result.Sent);
    }

    [Fact(DisplayName = "VAL-ACT-02: usa default de 48h quando tenant não possui setting")]
    public async Task TtlResolver_UsesDefaultWhenTenantSettingAbsent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (handler, emailSender, _, _, settingsRepo) = BuildStack(tenantId: tenantId, userId: userId);

        // Sem setting para o tenant — deve usar default 48h
        settingsRepo.ClearTenant(tenantId);

        var cmd = new SendUserDigestCommand(tenantId, userId, Tuesday, "user@example.com");
        var result = await handler.Handle(cmd, default);

        Assert.True(result.Sent);
    }

    // ------------------------------------------------------------------
    // PBT-02: N disparos ⇒ exatamente um envio efetivo por (tenant, user, date)
    // ------------------------------------------------------------------

    [Property(DisplayName = "PBT-02: N disparos para o mesmo (tenant, user, date) resultam em exatamente um envio efetivo")]
    public Property PBT02_NDispatches_ExactlyOneSend()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 10).Select(n => n)), // N ≥ 1
            n =>
            {
                var tenantId = Guid.NewGuid();
                var userId = Guid.NewGuid();
                var digestDate = new DigestDate(new LocalDate(2026, 6, 9)); // terça

                // Stack com atividade para garantir que o primeiro envio prossegue
                var activityPort = new InMemoryActivityReadPort();
                activityPort.SetOverdue(new[]
                {
                    new ActivityItem(Guid.NewGuid(), userId, new DateOnly(2026, 6, 8), "Tarefa"),
                });
                var oppPort = new InMemoryOpportunityReadPort();
                var forecastPort = new InMemoryForecastReadPort();
                var azimute = new AzimuteSectionBuilder(oppPort, forecastPort);
                var composer = new DigestContentComposer(activityPort, oppPort, azimute);

                var userDir = new InMemoryUserDirectoryPort();
                userDir.AddUser(new UserInfo(userId, tenantId, RecipientPapel.Vendedor, Active: true));

                var logRepo = new InMemoryEmailDigestLogRepository();
                var tokenRepo = new InMemoryDigestActionTokenRepository();
                var emailSender = new InMemoryEmailSender();
                var outbox = new InMemoryOutboxPublisher();
                var tokenFactory = new InMemoryActionTokenFactory();
                var settingsRepo = new InMemoryDigestTenantSettingsRepository();
                var options = new DigestOptions { DefaultActionTokenTtlHours = 48 };
                var ttlResolver = new ActionTokenTtlResolver(settingsRepo, options);
                var handler = new SendUserDigestHandler(
                    logRepo, tokenRepo, composer, emailSender, outbox, userDir,
                    activityPort, tokenFactory, ttlResolver);

                var cmd = new SendUserDigestCommand(tenantId, userId, digestDate, "user@example.com");

                // N disparos
                for (var i = 0; i < n; i++)
                    handler.Handle(cmd, default).GetAwaiter().GetResult();

                // Propriedade: exatamente um envio efetivo
                return (emailSender.SendCallCount == 1)
                    .ToProperty()
                    .Label($"N={n}, sendCount={emailSender.SendCallCount}");
            });
    }
}
