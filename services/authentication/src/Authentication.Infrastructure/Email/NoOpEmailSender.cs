using Authentication.Application.Ports;

namespace Authentication.Infrastructure.Email;

/// <summary>
/// Implementação no-op do <see cref="IEmailSender"/> para uso em desenvolvimento.
///
/// Não envia e-mails reais. Deve ser substituído pelo adapter real do
/// módulo <c>notification-delivery</c> em produção (design.md § 6.4).
///
/// Mapeia: TASK-08, TASK-09, TASK-19, TASK-20.
/// </summary>
public sealed class NoOpEmailSender : IEmailSender
{
    /// <inheritdoc/>
    public Task SendInviteEmailAsync(
        string email,
        string activationUrl,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // No-op: não envia e-mail real neste adapter
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendPasswordResetEmailAsync(
        string email,
        string resetUrl,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // No-op: não envia e-mail real neste adapter
        return Task.CompletedTask;
    }
}
