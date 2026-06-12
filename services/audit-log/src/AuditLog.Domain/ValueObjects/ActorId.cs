namespace AuditLog.Domain.ValueObjects;

/// <summary>
/// Identificador do autor da operação auditada (usuário humano ou de sistema).
/// Corresponde ao campo <c>user_id</c> na tabela <c>audit_logs</c>.
/// Imutável; igualdade por valor.
/// </summary>
public sealed record ActorId
{
    /// <summary>Valor interno do identificador.</summary>
    public Guid Value { get; }

    private ActorId(Guid value) => Value = value;

    /// <summary>
    /// Cria um <see cref="ActorId"/> a partir de um <see cref="Guid"/> existente.
    /// </summary>
    /// <param name="value">Guid não vazio que identifica o autor.</param>
    /// <exception cref="ArgumentException">Se <paramref name="value"/> for <see cref="Guid.Empty"/>.</exception>
    public static ActorId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(
                "O identificador de ator não pode ser um Guid vazio (REQ-002.2).",
                nameof(value));

        return new(value);
    }
}
