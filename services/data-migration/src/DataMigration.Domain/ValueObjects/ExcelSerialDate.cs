namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Objeto de valor que encapsula a conversão de serial Excel para <see cref="DateOnly"/> ISO
/// e o round-trip inverso.
///
/// Epoch: <b>1899-12-30</b> — acomoda o bug histórico do ano-1900 do Excel
/// (o Excel trata 1900 como ano bissexto, gerando o serial 60 = 28/fev/1900;
/// usar 1899-12-30 como epoch corrige automaticamente esse deslocamento).
///
/// Comportamento (design §4.3, DD-006, Req 9):
///   - Serial &lt;= 0 ou <c>null</c> → data nula sem exceção (célula vazia).
///   - Serial 1..N → <see cref="DateOnly"/> via epoch 1899-12-30.
///   - <see cref="IsOverdue"/> = <c>true</c> quando a data é anterior à data atual.
///   - <see cref="ToSerial()"/> reproduz o serial original (round-trip determinístico, PBT-03).
///
/// Imutável; igualdade por valor (<see cref="Value"/>).
///
/// Rastreia: design §4.3, DD-006, Req 9, PBT-03, TASK-04.
/// </summary>
public sealed class ExcelSerialDate : IEquatable<ExcelSerialDate>
{
    // =========================================================================
    // Epoch Excel (DD-006)
    // A epoch 1899-12-30 é a origem correta para o sistema de datas do Excel no Windows.
    // O Excel considera 1900-01-01 como serial 1, e por ter um bug que trata 1900 como
    // bissexto (adicionando o falso serial 60 = 29/fev/1900), o epoch efetivo para
    // round-trip correto é 1899-12-30 (que é 2 dias antes de 1900-01-01).
    // =========================================================================
    private static readonly DateOnly ExcelEpoch = new(1899, 12, 30);

    /// <summary>
    /// Data ISO resultante da conversão do serial Excel.
    /// </summary>
    public DateOnly Value { get; }

    /// <summary>
    /// <c>true</c> quando a data é anterior à data de referência (padrão: hoje UTC).
    /// Indica que a oportunidade tinha prazo vencido na planilha original.
    /// Rastreia: design §4.3, Req 9.
    /// </summary>
    public bool IsOverdue { get; }

    private ExcelSerialDate(DateOnly value, DateOnly today)
    {
        Value = value;
        IsOverdue = value < today;
    }

    // =========================================================================
    // Fábrica
    // =========================================================================

    /// <summary>
    /// Converte um serial Excel para <see cref="ExcelSerialDate"/>.
    ///
    /// Retorna <c>null</c> quando o serial é <c>null</c> ou ≤ 0 (célula vazia).
    /// </summary>
    /// <param name="serial">Serial Excel (número inteiro da célula da planilha).</param>
    /// <param name="today">
    /// Data de referência para cálculo de <see cref="IsOverdue"/>
    /// (injetado para testabilidade; padrão: hoje em UTC).
    /// </param>
    public static ExcelSerialDate? FromSerial(int? serial, DateOnly? today = null)
    {
        if (serial is null || serial <= 0)
        {
            return null;
        }

        var date = ExcelEpoch.AddDays(serial.Value);
        var reference = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return new ExcelSerialDate(date, reference);
    }

    // =========================================================================
    // Round-trip (PBT-03)
    // =========================================================================

    /// <summary>
    /// Converte a data ISO de volta para o serial Excel original.
    /// Garante round-trip determinístico (PBT-03, DD-006).
    /// </summary>
    public int ToSerial()
    {
        return Value.DayNumber - ExcelEpoch.DayNumber;
    }

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    /// <inheritdoc />
    public bool Equals(ExcelSerialDate? other)
    {
        if (other is null)
        {
            return false;
        }

        return Value == other.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ExcelSerialDate);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Value.ToString("yyyy-MM-dd");

    /// <summary>Operador de igualdade por valor.</summary>
    public static bool operator ==(ExcelSerialDate? left, ExcelSerialDate? right) =>
        Equals(left, right);

    /// <summary>Operador de desigualdade por valor.</summary>
    public static bool operator !=(ExcelSerialDate? left, ExcelSerialDate? right) =>
        !Equals(left, right);
}
