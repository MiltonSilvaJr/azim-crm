namespace Digest.Application.Repositories;

/// <summary>
/// Resultado da tentativa de reserva de idempotência em <see cref="IEmailDigestLogRepository.ReserveAsync"/>.
/// </summary>
public enum ReservationResult
{
    /// <summary>
    /// Reserva criada com sucesso — este worker venceu a corrida e deve prosseguir com o envio.
    /// </summary>
    Reserved,

    /// <summary>
    /// Reserva já existente com status terminal positivo (sent/delivered/opened) —
    /// e-mail já foi enviado; não reenvia (Req 9.2).
    /// </summary>
    AlreadySent,

    /// <summary>
    /// Reserva já existente com status scheduled (outro worker está processando) —
    /// aborta para evitar duplicação (RNF 2.3).
    /// </summary>
    AlreadyScheduled,
}
