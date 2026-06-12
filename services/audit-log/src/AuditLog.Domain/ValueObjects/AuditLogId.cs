namespace AuditLog.Domain.ValueObjects;

/// <summary>
/// Identificador único de um registro de auditoria.
/// Imutável; igualdade por valor.
/// </summary>
public sealed record AuditLogId
{
    /// <summary>Valor interno do identificador.</summary>
    public Guid Value { get; }

    private AuditLogId(Guid value) => Value = value;

    /// <summary>
    /// Cria um novo <see cref="AuditLogId"/> com um UUID gerado aleatoriamente.
    /// </summary>
    public static AuditLogId New() => new(Guid.NewGuid());

    /// <summary>
    /// Cria um <see cref="AuditLogId"/> a partir de um <see cref="Guid"/> existente.
    /// </summary>
    /// <param name="value">Guid não vazio que identifica o registro.</param>
    /// <exception cref="ArgumentException">Se <paramref name="value"/> for <see cref="Guid.Empty"/>.</exception>
    public static AuditLogId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(
                "O identificador de registro de auditoria não pode ser um Guid vazio.",
                nameof(value));

        return new(value);
    }
}
