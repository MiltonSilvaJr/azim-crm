using System.Text.RegularExpressions;
using OpportunityPipeline.Domain.Opportunities.Exceptions;

namespace OpportunityPipeline.Domain.Opportunities.ValueObjects;

/// <summary>
/// Identificador humano de oportunidade no formato AZ-NNNN (mínimo 4 dígitos).
/// Imutável após criação (INV-5). Gerado apenas pelo sistema (Req 3).
/// Mapeia: Req 3, INV-5, PBT-02, design §4.3.
/// </summary>
public sealed record OpportunityNumber
{
    private static readonly Regex FormatRegex = new(
        @"^AZ-\d{4,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Valor formatado, ex.: "AZ-0001".</summary>
    public string Value { get; }

    /// <summary>
    /// Cria instância validando o formato AZ-NNNN.
    /// </summary>
    /// <param name="value">String no formato AZ-NNNN (prefixo AZ-, mínimo 4 dígitos).</param>
    /// <exception cref="DomainException">Se o formato for inválido.</exception>
    public OpportunityNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !FormatRegex.IsMatch(value))
            throw new DomainException(
                $"OpportunityNumber inválido: '{value}'. Formato esperado: AZ-NNNN (mínimo 4 dígitos).");
        Value = value;
    }

    /// <summary>Cria OpportunityNumber a partir de um número sequencial.</summary>
    public static OpportunityNumber From(long sequenceNumber) =>
        new($"AZ-{sequenceNumber:0000}");

    /// <summary>Retorna o valor formatado.</summary>
    public override string ToString() => Value;
}
