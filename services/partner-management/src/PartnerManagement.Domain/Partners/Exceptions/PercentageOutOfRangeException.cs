namespace PartnerManagement.Domain.Partners.Exceptions;

/// <summary>
/// Lançada quando um percentual de comissão está fora do intervalo fechado [0,00; 100,00]
/// ou possui mais de 2 casas decimais.
/// Mapeia: Req 6.3, RNF 6, design §4.3, PM-ERR-003.
/// </summary>
public sealed class PercentageOutOfRangeException : DomainException
{
    /// <summary>
    /// Inicializa uma nova instância de <see cref="PercentageOutOfRangeException"/>.
    /// </summary>
    public PercentageOutOfRangeException()
        : base("PM-ERR-003", "Percentual fora do intervalo permitido. Informe um valor entre 0,00 e 100,00.")
    {
    }
}
