namespace AuditLog.Domain.ValueObjects;

/// <summary>
/// Identificador único de um tenant. Chave de RLS (Row-Level Security).
/// Imutável; igualdade por valor.
/// </summary>
public sealed record TenantId
{
    /// <summary>Valor interno do identificador.</summary>
    public Guid Value { get; }

    private TenantId(Guid value) => Value = value;

    /// <summary>
    /// Cria um <see cref="TenantId"/> a partir de um <see cref="Guid"/> existente.
    /// </summary>
    /// <param name="value">Guid não vazio que identifica o tenant.</param>
    /// <exception cref="ArgumentException">Se <paramref name="value"/> for <see cref="Guid.Empty"/>.</exception>
    public static TenantId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(
                "O identificador de tenant não pode ser um Guid vazio.",
                nameof(value));

        return new(value);
    }
}
