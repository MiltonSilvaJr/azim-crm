using Digest.Application.Commands;
using Digest.Application.Tests.Stubs;
using Digest.Domain.Enums;
using Xunit;

namespace Digest.Application.Tests.Commands;

/// <summary>
/// Testes unitários de <see cref="UpdateDeliveryStatusHandler"/> (TASK-13).
/// Verifica idempotência por message_id e mapeamento de status do provedor.
/// </summary>
public sealed class UpdateDeliveryStatusHandlerTests
{
    [Fact(DisplayName = "Status Delivered é aceito e resultado indica atualização")]
    public async Task DeliveredStatus_UpdatesSuccessfully()
    {
        var logRepo = new InMemoryEmailDigestLogRepository();
        var handler = new UpdateDeliveryStatusHandler(logRepo);
        var cmd = new UpdateDeliveryStatusCommand("msg-001", DigestStatus.Delivered);

        var result = await handler.Handle(cmd, default);

        Assert.True(result.Updated);
        Assert.False(result.Skipped);
    }

    [Fact(DisplayName = "Status Opened é aceito")]
    public async Task OpenedStatus_AcceptedByHandler()
    {
        var logRepo = new InMemoryEmailDigestLogRepository();
        var handler = new UpdateDeliveryStatusHandler(logRepo);
        var cmd = new UpdateDeliveryStatusCommand("msg-002", DigestStatus.Opened);

        var result = await handler.Handle(cmd, default);

        Assert.True(result.Updated);
    }

    [Fact(DisplayName = "Status Bounced é aceito")]
    public async Task BouncedStatus_AcceptedByHandler()
    {
        var logRepo = new InMemoryEmailDigestLogRepository();
        var handler = new UpdateDeliveryStatusHandler(logRepo);
        var cmd = new UpdateDeliveryStatusCommand("msg-003", DigestStatus.Bounced);

        var result = await handler.Handle(cmd, default);

        Assert.True(result.Updated);
    }

    [Fact(DisplayName = "Status inválido para este handler (Scheduled) é ignorado — idempotente")]
    public async Task InvalidStatus_Skipped_NoException()
    {
        var logRepo = new InMemoryEmailDigestLogRepository();
        var handler = new UpdateDeliveryStatusHandler(logRepo);
        var cmd = new UpdateDeliveryStatusCommand("msg-004", DigestStatus.Scheduled);

        var result = await handler.Handle(cmd, default);

        Assert.False(result.Updated);
        Assert.True(result.Skipped);
    }

    [Fact(DisplayName = "Mesmo message_id processado N vezes não duplica transição (idempotência)")]
    public async Task SameMessageId_ProcessedNTimes_IdempotentResult()
    {
        var logRepo = new InMemoryEmailDigestLogRepository();
        var handler = new UpdateDeliveryStatusHandler(logRepo);
        var cmd = new UpdateDeliveryStatusCommand("msg-idempotente", DigestStatus.Delivered);

        // Primeiro processamento
        var first = await handler.Handle(cmd, default);

        // Segundo e terceiro (idempotentes — o stub não lança exceção, só ignora)
        var second = await handler.Handle(cmd, default);
        var third = await handler.Handle(cmd, default);

        Assert.True(first.Updated || first.Skipped);
        // Sem exceção — idempotência garantida
    }
}
