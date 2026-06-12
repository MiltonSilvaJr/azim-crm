using AuditLog.Application.Commands;
using AuditLog.Application.Writers;
using AuditLog.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.Writers;

/// <summary>
/// Testes unitários do <see cref="AuditServiceWriter"/>.
/// Verifica o mapeamento de <see cref="AuditEntryRequest"/> → <see cref="RecordAuditEntryCommand"/>
/// e o dispatch via MediatR.
/// </summary>
public sealed class AuditServiceWriterTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    private AuditServiceWriter BuildSut() =>
        new(_sender, NullLogger<AuditServiceWriter>.Instance);

    // -----------------------------------------------------------------------
    // Dispatch via MediatR
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "RecordAsync deve despachar RecordAuditEntryCommand via ISender")]
    public async Task RecordAsync_Should_Send_Command_Via_Sender()
    {
        var sut = BuildSut();
        var request = ValidRequest();

        await sut.RecordAsync(request, CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Any<RecordAuditEntryCommand>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Mapeamento de campos
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "RecordAsync deve mapear EntityType corretamente")]
    public async Task RecordAsync_Should_Map_EntityType()
    {
        var sut = BuildSut();
        RecordAuditEntryCommand? captured = null;
        await _sender.Send(
            Arg.Do<RecordAuditEntryCommand>(c => captured = c),
            Arg.Any<CancellationToken>());

        await sut.RecordAsync(ValidRequest(), CancellationToken.None);

        captured!.EntityType.Should().Be("Opportunity");
    }

    [Fact(DisplayName = "RecordAsync deve mapear EntityId como Guid")]
    public async Task RecordAsync_Should_Map_EntityId_As_Guid()
    {
        var sut = BuildSut();
        var entityId = Guid.NewGuid();
        var request = ValidRequest() with { EntityId = entityId.ToString() };

        RecordAuditEntryCommand? captured = null;
        await _sender.Send(
            Arg.Do<RecordAuditEntryCommand>(c => captured = c),
            Arg.Any<CancellationToken>());

        await sut.RecordAsync(request, CancellationToken.None);

        captured!.EntityId.Should().Be(entityId);
    }

    [Fact(DisplayName = "RecordAsync deve mapear ActorId como Guid")]
    public async Task RecordAsync_Should_Map_UserId_As_Guid()
    {
        var sut = BuildSut();
        var userId = Guid.NewGuid();
        var request = ValidRequest() with { UserId = userId.ToString() };

        RecordAuditEntryCommand? captured = null;
        await _sender.Send(
            Arg.Do<RecordAuditEntryCommand>(c => captured = c),
            Arg.Any<CancellationToken>());

        await sut.RecordAsync(request, CancellationToken.None);

        captured!.ActorId.Should().Be(userId);
    }

    [Theory(DisplayName = "RecordAsync deve mapear AuditAction corretamente")]
    [InlineData(AuditAction.Create)]
    [InlineData(AuditAction.Update)]
    [InlineData(AuditAction.Delete)]
    public async Task RecordAsync_Should_Map_Action(AuditAction contractAction)
    {
        var sut = BuildSut();
        RecordAuditEntryCommand? captured = null;
        await _sender.Send(
            Arg.Do<RecordAuditEntryCommand>(c => captured = c),
            Arg.Any<CancellationToken>());

        var request = BuildRequestForAction(contractAction);
        await sut.RecordAsync(request, CancellationToken.None);

        var expectedDomainAction = contractAction switch
        {
            AuditAction.Create => Domain.ValueObjects.AuditAction.Create,
            AuditAction.Update => Domain.ValueObjects.AuditAction.Update,
            AuditAction.Delete => Domain.ValueObjects.AuditAction.Delete,
            _ => throw new ArgumentOutOfRangeException()
        };

        captured!.Action.Should().Be(expectedDomainAction);
    }

    // -----------------------------------------------------------------------
    // Validação de campos
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "RecordAsync deve lançar ArgumentException para EntityId não-UUID")]
    public async Task RecordAsync_Should_Throw_For_Invalid_EntityId()
    {
        var sut = BuildSut();
        var request = ValidRequest() with { EntityId = "not-a-guid" };

        var act = async () => await sut.RecordAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "RecordAsync deve lançar ArgumentException para UserId não-UUID")]
    public async Task RecordAsync_Should_Throw_For_Invalid_UserId()
    {
        var sut = BuildSut();
        var request = ValidRequest() with { UserId = "not-a-guid" };

        var act = async () => await sut.RecordAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(DisplayName = "RecordAsync deve lançar ArgumentNullException para request nulo")]
    public async Task RecordAsync_Should_Throw_For_Null_Request()
    {
        var sut = BuildSut();

        var act = async () => await sut.RecordAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static AuditEntryRequest ValidRequest() => new(
        EntityType: "Opportunity",
        EntityId: Guid.NewGuid().ToString(),
        Action: AuditAction.Create,
        UserId: Guid.NewGuid().ToString(),
        RawBefore: null,
        RawAfter: new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" });

    private static AuditEntryRequest BuildRequestForAction(AuditAction action) =>
        action switch
        {
            AuditAction.Create => ValidRequest(),
            AuditAction.Update => new AuditEntryRequest(
                EntityType: "Opportunity",
                EntityId: Guid.NewGuid().ToString(),
                Action: AuditAction.Update,
                UserId: Guid.NewGuid().ToString(),
                RawBefore: new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "A" },
                RawAfter: new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "B" }),
            AuditAction.Delete => new AuditEntryRequest(
                EntityType: "Opportunity",
                EntityId: Guid.NewGuid().ToString(),
                Action: AuditAction.Delete,
                UserId: Guid.NewGuid().ToString(),
                RawBefore: new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Old" },
                RawAfter: null),
            _ => ValidRequest()
        };
}
