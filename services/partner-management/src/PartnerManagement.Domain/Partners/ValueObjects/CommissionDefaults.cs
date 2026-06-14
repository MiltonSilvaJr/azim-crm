namespace PartnerManagement.Domain.Partners.ValueObjects;

/// <summary>
/// Objeto de valor que representa os percentuais padrão de comissão de um parceiro:
/// <c>pct_setup</c> e <c>pct_recorrente</c>, ambos no intervalo [0,00; 100,00].
/// Imutável, com igualdade por valor.
/// Default é 0,00 / 0,00 (parceiro pós-importação em triagem).
/// <c>IsTriagePending</c> é derivado (não persistido) — verdadeiro quando ambos são 0,00.
/// Mapeia: Req 6, PBT-03, design §4.3, design §4.6, DD-004.
/// </summary>
public sealed class CommissionDefaults : IEquatable<CommissionDefaults>
{
    /// <summary>Instância default com ambos os percentuais em 0,00 (estado de triagem).</summary>
    public static readonly CommissionDefaults Default =
        new(Percentage.Create(0.00m), Percentage.Create(0.00m));

    /// <summary>Percentual padrão de setup da comissão.</summary>
    public Percentage PctSetup { get; }

    /// <summary>Percentual padrão de recorrência da comissão.</summary>
    public Percentage PctRecorrente { get; }

    /// <summary>
    /// Indica se o parceiro está pendente de triagem de percentuais.
    /// Derivado: verdadeiro quando ambos os percentuais são 0,00 (Req 11.1, design §4.6).
    /// Não é campo persistido.
    /// </summary>
    public bool IsTriagePending =>
        PctSetup.Value == 0.00m && PctRecorrente.Value == 0.00m;

    private CommissionDefaults(Percentage pctSetup, Percentage pctRecorrente)
    {
        PctSetup = pctSetup;
        PctRecorrente = pctRecorrente;
    }

    /// <summary>
    /// Cria um <see cref="CommissionDefaults"/> com os percentuais fornecidos.
    /// </summary>
    /// <param name="pctSetup">Percentual de setup.</param>
    /// <param name="pctRecorrente">Percentual de recorrência.</param>
    public static CommissionDefaults Create(Percentage pctSetup, Percentage pctRecorrente)
    {
        ArgumentNullException.ThrowIfNull(pctSetup);
        ArgumentNullException.ThrowIfNull(pctRecorrente);
        return new CommissionDefaults(pctSetup, pctRecorrente);
    }

    /// <inheritdoc/>
    public bool Equals(CommissionDefaults? other) =>
        other is not null && PctSetup == other.PctSetup && PctRecorrente == other.PctRecorrente;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CommissionDefaults other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(PctSetup, PctRecorrente);

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(CommissionDefaults? left, CommissionDefaults? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(CommissionDefaults? left, CommissionDefaults? right) => !(left == right);

    /// <inheritdoc/>
    public override string ToString() =>
        $"Setup={PctSetup}, Recorrente={PctRecorrente}";
}
