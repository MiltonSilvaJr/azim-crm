using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Authentication.Domain.Specifications;

namespace Authentication.Application.Services;

/// <summary>
/// Serviço de aplicação que coordena a criação e ativação de convites de usuário.
///
/// Casos de uso:
/// - <see cref="CreateAsync"/>: gera link de ativação (TTL 72h — DD-009) via IdP
///   e dispara e-mail; falha de e-mail → <see cref="InviteEmailFailedException"/>
///   (AUTH-ERR-032) sem reverter convite no IdP (Req 7.2).
/// - <see cref="ActivateAsync"/>: aplica <c>InviteUsableSpec</c>; link expirado/consumido
///   → AUTH-ERR-033 (410); link válido → emite evento auditável (PBT-05, Req 7.4/7.5).
///
/// Stateless: não persiste estado de convite; o estado vive no IdP e no organization.
///
/// Mapeia: TASK-08, design.md § 5.1, § 5.3, Req 7, DD-009, PBT-05.
/// </summary>
public sealed class InviteActivationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserDirectory _userDirectory;
    private readonly IEmailSender _emailSender;
    private readonly IAuditEventEmitter _auditEmitter;

    /// <summary>
    /// Inicializa o serviço com as portas de saída necessárias.
    /// </summary>
    public InviteActivationService(
        IIdentityProvider identityProvider,
        IUserDirectory userDirectory,
        IEmailSender emailSender,
        IAuditEventEmitter auditEmitter)
    {
        _identityProvider = identityProvider;
        _userDirectory = userDirectory;
        _emailSender = emailSender;
        _auditEmitter = auditEmitter;
    }

    /// <summary>
    /// Cria um convite: verifica e-mail duplicado, gera link no IdP e dispara e-mail.
    ///
    /// Se o e-mail já está ativo → lança <see cref="IdentityProviderException"/> AUTH-ERR-030.
    /// Se o e-mail falha → lança <see cref="InviteEmailFailedException"/> AUTH-ERR-032
    ///   (convite NO IdP permanece válido — Req 7.2).
    /// </summary>
    /// <param name="command">Dados do convite.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Link de ativação gerado.</returns>
    public async Task<ActivationLinkResult> CreateAsync(
        CreateInviteCommand command,
        CancellationToken cancellationToken = default)
    {
        // Verificar e-mail duplicado (Req 7.6, AUTH-ERR-030)
        var isActive = await _userDirectory.IsEmailActiveAsync(
            command.Email, command.TenantId, cancellationToken);

        if (isActive)
        {
            throw new IdentityProviderException(
                "AUTH-ERR-030",
                $"E-mail já cadastrado e ativo no tenant (Req 7.6).");
        }

        // Gerar link de ativação no IdP (TTL DD-009: 72h)
        var activationLink = await _identityProvider.GenerateInviteActivationAsync(
            command.Email,
            command.FirebaseTenant,
            cancellationToken);

        // Disparar e-mail — falha não reverte convite (Req 7.2)
        try
        {
            await _emailSender.SendInviteEmailAsync(
                command.Email,
                activationLink.ActivationUrl,
                command.TenantId,
                cancellationToken);
        }
        catch (EmailSenderException ex)
        {
            // Convite criado no IdP mas e-mail não pôde ser enviado → AUTH-ERR-032
            throw new InviteEmailFailedException(activationLink, ex);
        }

        return activationLink;
    }

    /// <summary>
    /// Ativa uma conta via link de convite.
    ///
    /// Aplica <c>InviteUsableSpec</c>: link expirado ou consumido → AUTH-ERR-033 (410).
    /// Link válido → emite evento auditável <c>invite_activated</c>.
    ///
    /// PBT-05: estados <c>expired</c> e <c>consumed</c> são terminais —
    /// nenhuma sequência de operações reabilita o link.
    /// </summary>
    /// <param name="command">Dados da ativação com estado e expiração do link.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task ActivateAsync(
        ActivateInviteCommand command,
        CancellationToken cancellationToken = default)
    {
        // Aplica InviteUsableSpec: verifica estado e prazo (PBT-05, Req 7.4/7.5)
        if (!InviteUsableSpec.IsSatisfiedBy(command.State, command.ExpiresAt, DateTimeOffset.UtcNow))
        {
            throw new IdentityProviderException(
                "AUTH-ERR-033",
                "Link de convite expirado ou já consumido (PBT-05, Req 7.4/7.5).");
        }

        // Emitir evento auditável apenas em ativação bem-sucedida
        await _auditEmitter.EmitAsync(
            eventType: "invite_activated",
            tenantId: command.TenantId,
            userId: command.UserId,
            cancellationToken: cancellationToken);
    }
}
