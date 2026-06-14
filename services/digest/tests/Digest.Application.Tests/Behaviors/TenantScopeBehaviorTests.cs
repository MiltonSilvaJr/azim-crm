using Digest.Application.Behaviors;
using Digest.Application.Commands;
using Digest.Domain.ValueObjects;
using NodaTime;
using Xunit;

namespace Digest.Application.Tests.Behaviors;

/// <summary>
/// Testes do <see cref="TenantScopeBehavior{TRequest, TResponse}"/> (TASK-13).
/// Verifica que comandos de tenant sem tenant_id válido são rejeitados.
/// </summary>
public sealed class TenantScopeBehaviorTests
{
    [Fact(DisplayName = "RunDigestForTenantCommand com tenant_id vazio lança exceção")]
    public async Task RunDigest_EmptyTenantId_ThrowsException()
    {
        var behavior = new TenantScopeBehavior<RunDigestForTenantCommand, RunDigestForTenantResult>();
        var cmd = new RunDigestForTenantCommand(Guid.Empty, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(cmd, () => Task.FromResult(new RunDigestForTenantResult(0, 0, 0, 0)), default));
    }

    [Fact(DisplayName = "SendUserDigestCommand com tenant_id vazio lança exceção")]
    public async Task SendUserDigest_EmptyTenantId_ThrowsException()
    {
        var behavior = new TenantScopeBehavior<SendUserDigestCommand, SendUserDigestResult>();
        var cmd = new SendUserDigestCommand(
            Guid.Empty, Guid.NewGuid(),
            new DigestDate(new LocalDate(2026, 6, 9)),
            "user@example.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(cmd, () => Task.FromResult(new SendUserDigestResult(false, false)), default));
    }

    [Fact(DisplayName = "RunDigestForTenantCommand com tenant_id válido prossegue sem exceção")]
    public async Task RunDigest_ValidTenantId_Proceeds()
    {
        var behavior = new TenantScopeBehavior<RunDigestForTenantCommand, RunDigestForTenantResult>();
        var cmd = new RunDigestForTenantCommand(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = await behavior.Handle(
            cmd,
            () => Task.FromResult(new RunDigestForTenantResult(1, 1, 0, 0)),
            default);

        Assert.Equal(1, result.RecipientsSelected);
    }
}
