using MediatR;

namespace Digest.Application.Commands;

/// <summary>
/// Comando que orquestra o digest completo para um tenant.
/// Disparado pelo <c>PerTenantConsumer</c> (mensagem Pub/Sub por tenant — DD-005, design §5.1).
/// </summary>
/// <param name="TenantId">Identificador do tenant a processar.</param>
/// <param name="ReferenceUtc">Instante UTC de referência do disparo (derivado do trigger horário).</param>
/// <param name="CorrelationId">Identificador de correlação para rastreabilidade (sem PII).</param>
public sealed record RunDigestForTenantCommand(
    Guid TenantId,
    DateTimeOffset ReferenceUtc,
    Guid? CorrelationId = null) : IRequest<RunDigestForTenantResult>;

/// <summary>
/// Resultado da orquestração do digest para um tenant.
/// </summary>
/// <param name="RecipientsSelected">Número de destinatários selecionados.</param>
/// <param name="EmailsSent">Número de e-mails efetivamente enviados.</param>
/// <param name="EmailsSkipped">Número de envios ignorados por idempotência.</param>
/// <param name="EmailsFailed">Número de falhas de envio individuais.</param>
public sealed record RunDigestForTenantResult(
    int RecipientsSelected,
    int EmailsSent,
    int EmailsSkipped,
    int EmailsFailed);
