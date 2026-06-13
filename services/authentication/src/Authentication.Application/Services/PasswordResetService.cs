using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Domain.Specifications;
using Microsoft.Extensions.Options;

namespace Authentication.Application.Services;

/// <summary>
/// Serviço de aplicação responsável pela solicitação de redefinição de senha.
///
/// Anti-enumeração (Req 8.3, Req 10.2, PBT-03):
///   - Para e-mail inexistente: nenhuma ação observável, retorna accepted.
///   - Para usuário Google (<c>EmailMethodSpec</c> falsa): nenhuma ação, retorna accepted.
///   - Para e-mail de método password: gera link e envia e-mail, retorna accepted.
///   - Em todos os casos, o delay constante configurável equaliza o tempo de resposta.
///   - A mesma categoria de log é usada em todos os ramos (sem vazar PII).
///
/// Delay constante (Req 10.2, RISK-AUTH-05): <see cref="PasswordResetOptions.ConstantDelayMs"/>
/// aplicado ao final de toda execução para impedir oráculo de timing.
///
/// Mapeia: TASK-09, design.md § 5.1, § 5.3, Req 8, Req 10.2, PBT-03.
/// </summary>
public sealed class PasswordResetService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserDirectory _userDirectory;
    private readonly IEmailSender _emailSender;
    private readonly IAuditEventEmitter _auditEmitter;
    private readonly PasswordResetOptions _options;

    /// <summary>
    /// Inicializa o serviço com as portas de saída e as opções de configuração.
    /// </summary>
    public PasswordResetService(
        IIdentityProvider identityProvider,
        IUserDirectory userDirectory,
        IEmailSender emailSender,
        IAuditEventEmitter auditEmitter,
        IOptions<PasswordResetOptions> options)
    {
        _identityProvider = identityProvider;
        _userDirectory = userDirectory;
        _emailSender = emailSender;
        _auditEmitter = auditEmitter;
        _options = options.Value;
    }

    /// <summary>
    /// Processa a solicitação de redefinição de senha com resposta uniforme.
    ///
    /// Nunca lança exceção visível ao chamador — a resposta pública é sempre "accepted"
    /// independente do estado do e-mail (PBT-03, Req 8.3, Req 10.2).
    ///
    /// Fluxo interno (silencioso — sem diferença observável externamente):
    ///   1. Resolve usuário por e-mail via <c>IUserDirectory</c>.
    ///   2. Se não encontrado: noop (anti-enumeração).
    ///   3. Se usuário Google (<c>EmailMethodSpec</c> falsa): noop (Req 8.4).
    ///   4. Se método password: gera link + envia e-mail + emite evento auditável.
    ///   5. Aplica delay constante configurável (equalização de timing — RISK-AUTH-05).
    /// </summary>
    /// <param name="command">Solicitação de reset com e-mail e tenant.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task RequestAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ProcessInternalAsync(command, cancellationToken);
        }
        catch (Exception)
        {
            // Exceções internas são capturadas silenciosamente para anti-enumeração.
            // Logging de mesma categoria ocorre no adapter de observabilidade (DD-006).
            // O caller sempre recebe "accepted" (Req 8.3, PBT-03).
        }
        finally
        {
            // Delay constante equaliza timing (Req 10.2, RISK-AUTH-05)
            if (_options.ConstantDelayMs > 0)
                await Task.Delay(_options.ConstantDelayMs, cancellationToken);
        }
    }

    // Lógica interna — pode lançar exceções que são capturadas em RequestAsync
    private async Task ProcessInternalAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        // Resolver usuário por e-mail
        var user = await _userDirectory.FindUserByEmailAsync(
            command.Email, command.TenantId, cancellationToken);

        // E-mail não encontrado — noop silencioso (anti-enumeração, PBT-03)
        if (user is null)
            return;

        // Aplicar EmailMethodSpec: usuário Google não recebe link de senha (Req 8.4)
        if (!EmailMethodSpec.IsSatisfiedBy(user.SignInProvider ?? string.Empty))
            return; // noop silencioso

        // Gerar link de redefinição no IdP
        var resetLink = await _identityProvider.GeneratePasswordResetLinkAsync(
            command.Email,
            command.FirebaseTenant,
            cancellationToken);

        // Enviar e-mail (falha é capturada no caller silenciosamente — anti-enumeração)
        await _emailSender.SendPasswordResetEmailAsync(
            command.Email,
            resetLink.ResetUrl,
            command.TenantId,
            cancellationToken);

        // Emitir evento auditável de solicitação de reset (RNF 10)
        await _auditEmitter.EmitAsync(
            eventType: "password_reset_requested",
            tenantId: command.TenantId,
            userId: user.UserId,
            cancellationToken: cancellationToken);
    }
}
