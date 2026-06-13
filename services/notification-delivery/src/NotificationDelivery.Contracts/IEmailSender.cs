using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NotificationDelivery.Contracts;

/// <summary>
/// Interface pública estável para envio de e-mails transacionais.
///
/// Único ponto de acoplamento entre os módulos consumidores e o adapter notification-delivery.
/// Nenhum tipo de provedor (Postmark, SendGrid, Resend) aparece nesta assinatura (Req 1.2, RNF 1).
///
/// Contrato semântico (design §8.1, Req 1, Req 3):
/// <list type="bullet">
///   <item><description><see cref="SendAsync"/> nunca lança exceção de provedor — toda falha é um <see cref="SendResult"/> com <see cref="SendStatus"/> (Req 3.5, PBT-04).</description></item>
///   <item><description>Substituível por implementação de teste sem referenciar provedor (Req 1.5, RNF-1.2).</description></item>
///   <item><description>Seleção de implementação concreta via configuração e DI, sem alteração de código dos consumidores (Req 4).</description></item>
/// </list>
///
/// Implementado em Infrastructure por <c>PostmarkEmailSender</c>, <c>SendGridEmailSender</c>
/// ou <c>ResendEmailSender</c>, com resiliência adicionada pelo <c>ResilientEmailSender</c> em Application.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envia uma mensagem de e-mail de forma assíncrona.
    ///
    /// <para>Nunca lança exceção ao chamador — toda falha de provedor é mapeada para um
    /// <see cref="SendResult"/> com o <see cref="SendStatus"/> e <see cref="FailureReason"/>
    /// canônicos (Req 3.5, PBT-04).</para>
    /// </summary>
    /// <param name="message">Mensagem de e-mail válida e imutável.</param>
    /// <param name="cancellationToken">Token de cancelamento para controle de timeout e graceful shutdown.</param>
    /// <returns>
    /// <see cref="SendResult"/> com o estado canônico do resultado.
    /// Nunca retorna <c>null</c>.
    /// </returns>
    Task<SendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica a disponibilidade do provedor de e-mail com um ping leve, sem enviar e-mail real
    /// nem consumir cota de envio (Req 11, Req 11.2).
    ///
    /// <para>Consumido pelo <c>GET /health/ready</c> do <c>azim-digest-worker</c> (TRD §14.5).</para>
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// <see cref="HealthCheckResult"/> indicando disponibilidade do provedor.
    /// Falha reportada sem PII e sem credencial (Req 11.3).
    /// </returns>
    Task<HealthCheckResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
}
