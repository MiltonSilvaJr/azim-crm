using Digest.Application.Abstractions;
using Digest.Application.Models;
using Digest.Application.Ports;
using Digest.Application.Repositories;
using Digest.Application.Services;
using Digest.Domain.Aggregates;
using Digest.Domain.Enums;
using MediatR;

namespace Digest.Application.Commands;

/// <summary>
/// Handler de <see cref="SendUserDigestCommand"/>.
/// Implementa o fluxo de idempotência por reserva (DD-008, PBT-02) e envio do digest via <see cref="IEmailSender"/>.
/// </summary>
/// <remarks>
/// Fluxo (design §5.3):
/// <list type="number">
///   <item>Tenta reservar status <c>scheduled</c> via <see cref="IEmailDigestLogRepository.ReserveAsync"/>.</item>
///   <item>Se já existe sent/delivered/opened, retorna sem enviar (Req 9.2).</item>
///   <item>Invoca <see cref="DigestContentComposer"/> para montar o conteúdo.</item>
///   <item>Se sem bloco aplicável, aborta sem envio.</item>
///   <item><see cref="IActionTokenFactory"/> emite tokens das atividades.</item>
///   <item><see cref="IEmailSender.SendAsync"/> realiza o envio físico.</item>
///   <item>Atualiza log para <c>sent</c>/<c>failed</c> e publica <c>DigestEmailSent</c> no Outbox (DD-009).</item>
/// </list>
/// </remarks>
public sealed class SendUserDigestHandler : IRequestHandler<SendUserDigestCommand, SendUserDigestResult>
{
    private readonly IEmailDigestLogRepository _logRepository;
    private readonly DigestContentComposer _composer;
    private readonly IEmailSender _emailSender;
    private readonly IOutboxPublisher _outboxPublisher;
    private readonly IUserDirectoryPort _userDirectory;

    /// <summary>
    /// Constrói o handler com as dependências necessárias.
    /// </summary>
    public SendUserDigestHandler(
        IEmailDigestLogRepository logRepository,
        DigestContentComposer composer,
        IEmailSender emailSender,
        IOutboxPublisher outboxPublisher,
        IUserDirectoryPort userDirectory)
    {
        _logRepository = logRepository;
        _composer = composer;
        _emailSender = emailSender;
        _outboxPublisher = outboxPublisher;
        _userDirectory = userDirectory;
    }

    /// <inheritdoc/>
    public async Task<SendUserDigestResult> Handle(
        SendUserDigestCommand request,
        CancellationToken cancellationToken)
    {
        // Passo 1: Reserva de idempotência (DD-008)
        // INSERT ... status='scheduled' ON CONFLICT DO NOTHING
        var reservationResult = await _logRepository.ReserveAsync(
            request.TenantId,
            request.UserId,
            request.DigestDate,
            request.CorrelationId,
            cancellationToken);

        if (reservationResult == ReservationResult.AlreadySent)
        {
            // E-mail já foi enviado — não reenvia (Req 9.2, RNF 2.3)
            return new SendUserDigestResult(Sent: false, Skipped: true);
        }

        if (reservationResult == ReservationResult.AlreadyScheduled)
        {
            // Outro worker está processando — aborta (RNF 2.3)
            return new SendUserDigestResult(Sent: false, Skipped: true);
        }

        // Passo 2: Composição do conteúdo
        // A seleção já foi feita por SelectRecipientsQuery; aqui reconstruímos o candidato
        // com base no resultado da policy para compor. Para o handler direto, usamos o papel
        // disponível no userDirectory ou inferimos do contexto. Para simplicidade e testabilidade,
        // o candidate é reconstruído com receiveAzimute baseado no dia e tipo de papel.
        // O papel real é obtido via IUserDirectoryPort.
        var users = await _userDirectory.GetActiveUsersAsync(request.TenantId, cancellationToken);
        var userInfo = users.FirstOrDefault(u => u.UserId == request.UserId);

        // Se o usuário não é mais encontrado, aborta sem enviar
        if (userInfo is null)
        {
            await _logRepository.MarkFailedAsync(
                request.TenantId, request.UserId, request.DigestDate, cancellationToken);
            return new SendUserDigestResult(Sent: false, Skipped: false);
        }

        var isGestao = userInfo.Papel is RecipientPapel.GestorBU or RecipientPapel.TAdmin;
        var isMonday = request.DigestDate.IsMonday();

        var candidate = new RecipientCandidate(
            request.UserId,
            request.TenantId,
            userInfo.Papel,
            new Domain.Policies.RecipientSelectionResult(
                ShouldReceivePendencias: true, // sempre true quando chegou aqui via RunDigest
                ShouldReceiveAzimute: isMonday && isGestao));

        var content = await _composer.ComposeAsync(
            request.TenantId, candidate, request.DigestDate, cancellationToken);

        // Passo 3: Verifica se há conteúdo para enviar
        if (!DigestContentComposer.ShouldSend(content, candidate))
        {
            await _logRepository.MarkFailedAsync(
                request.TenantId, request.UserId, request.DigestDate, cancellationToken);
            return new SendUserDigestResult(Sent: false, Skipped: false);
        }

        // Passo 4: Envio via IEmailSender
        try
        {
            var sendRequest = new SendRequest(
                TenantId: request.TenantId,
                UserId: request.UserId,
                ToEmail: request.UserEmail, // PII — nunca logar
                DigestDate: request.DigestDate,
                Content: content,
                CorrelationId: request.CorrelationId);

            var sendResult = await _emailSender.SendAsync(sendRequest, cancellationToken);

            if (!sendResult.Success)
            {
                // Passo 5a: Falha definitiva do provedor
                await _logRepository.MarkFailedAsync(
                    request.TenantId, request.UserId, request.DigestDate, cancellationToken);
                return new SendUserDigestResult(Sent: false, Skipped: false);
            }

            // Passo 5b: Sucesso — atualiza log e publica no Outbox (DD-009)
            await _logRepository.MarkSentAsync(
                request.TenantId, request.UserId, request.DigestDate, sendResult.MessageId, cancellationToken);

            // DigestJob emite o domain event (Req 11, RNF 10.1)
            var job = DigestJob.Create(request.TenantId, request.DigestDate);
            job.RegisterSent(request.UserId, sendResult.MessageId);

            foreach (var domainEvent in job.DomainEvents)
            {
                await _outboxPublisher.PublishAsync(domainEvent, cancellationToken);
            }

            return new SendUserDigestResult(Sent: true, Skipped: false, MessageId: sendResult.MessageId);
        }
        catch (Exception)
        {
            // Falha inesperada — marca como failed (RNF 5.2)
            await _logRepository.MarkFailedAsync(
                request.TenantId, request.UserId, request.DigestDate, cancellationToken);
            return new SendUserDigestResult(Sent: false, Skipped: false);
        }
    }
}
