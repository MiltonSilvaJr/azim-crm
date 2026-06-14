namespace PartnerManagement.Application.Partners;

/// <summary>
/// Exceção lançada quando o período de comissão é inválido (PM-ERR-011).
/// Ocorre quando <c>from</c> >= <c>to</c> ou os valores são ausentes/malformados.
/// Mapeia: Req 9.7, design §12.
/// </summary>
public sealed class InvalidCommissionPeriodException : Exception
{
    /// <summary>Início do período informado.</summary>
    public DateTimeOffset From { get; }

    /// <summary>Fim do período informado.</summary>
    public DateTimeOffset To { get; }

    /// <summary>Inicializa a exceção com o período inválido.</summary>
    public InvalidCommissionPeriodException(DateTimeOffset from, DateTimeOffset to)
        : base($"Período de comissão inválido: from={from:O}, to={to:O}. 'from' deve ser anterior a 'to'.")
    {
        From = from;
        To = to;
    }
}
