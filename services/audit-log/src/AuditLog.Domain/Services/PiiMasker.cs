using AuditLog.Domain.ValueObjects;

namespace AuditLog.Domain.Services;

/// <summary>
/// Serviço de domínio puro responsável por mascarar campos PII no <see cref="AuditDelta"/>
/// antes da persistência do registro de auditoria.
/// <para>
/// Aplica a política configurada em <see cref="IPiiFieldPolicy"/> por <c>entity_type</c>.
/// Substitui o valor de campos PII pelo marcador <see cref="MaskedMarker"/>,
/// preservando a chave do campo no delta (REQ-004.4).
/// </para>
/// <para>
/// Não possui dependência de infraestrutura. A política é injetada via construtor (design §4.6).
/// </para>
/// </summary>
public sealed class PiiMasker
{
    /// <summary>
    /// Marcador que substitui valores PII mascarados.
    /// Preserva a presença da chave sem revelar o conteúdo (REQ-004.4).
    /// </summary>
    public const string MaskedMarker = "[MASKED]";

    private readonly IPiiFieldPolicy _policy;

    /// <summary>
    /// Cria um <see cref="PiiMasker"/> com a política de campos PII informada.
    /// </summary>
    /// <param name="policy">Política que mapeia tipos de entidade para campos PII.</param>
    /// <exception cref="ArgumentNullException">Se <paramref name="policy"/> for nulo.</exception>
    public PiiMasker(IPiiFieldPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    /// <summary>
    /// Mascara os campos PII do <paramref name="delta"/> para o tipo de entidade informado.
    /// Se o tipo não tiver política registrada, retorna o delta original sem modificação.
    /// </summary>
    /// <param name="entityType">Tipo da entidade (ex.: <c>Contact</c>).</param>
    /// <param name="delta">Delta a ser mascarado.</param>
    /// <returns>Delta com campos PII substituídos por <see cref="MaskedMarker"/>.</returns>
    /// <exception cref="ArgumentNullException">Se <paramref name="entityType"/> ou <paramref name="delta"/> for nulo.</exception>
    public AuditDelta Mask(string entityType, AuditDelta delta)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(delta);

        var piiFields = _policy.GetPiiFields(entityType);

        if (piiFields.Count == 0)
            return delta;

        return delta.Kind switch
        {
            AuditDeltaKind.Create => MaskCreate(delta, piiFields),
            AuditDeltaKind.Update => MaskUpdate(delta, piiFields),
            AuditDeltaKind.Delete => MaskDelete(delta, piiFields),
            _ => throw new InvalidOperationException(
                $"Tipo de delta desconhecido: {delta.Kind}. Verifique se AuditDeltaKind foi expandido.")
        };
    }

    // ------------------------------------------------------------------ Variantes privadas

    private static AuditDelta MaskCreate(AuditDelta delta, IReadOnlySet<string> piiFields)
    {
        var masked = MaskObjectDictionary(delta.After!, piiFields);
        return AuditDelta.ForCreate(masked);
    }

    private static AuditDelta MaskUpdate(AuditDelta delta, IReadOnlySet<string> piiFields)
    {
        // TransformChanges aplica a transformação sobre um delta já validado por ForUpdate.
        // O guard before != after foi verificado nos valores originais; não é re-executado aqui,
        // o que permite que dois PII distintos (ex.: "João" e "Maria") produzam o mesmo "[MASKED]"
        // sem falso positivo de "nenhuma mudança real" (design §4.3, REQ-004.4).
        return delta.TransformChanges((field, change) =>
            piiFields.Contains(field)
                ? new AuditAttributeChange(MaskedMarker, MaskedMarker)
                : change);
    }

    private static AuditDelta MaskDelete(AuditDelta delta, IReadOnlySet<string> piiFields)
    {
        var masked = MaskObjectDictionary(delta.Before!, piiFields);
        return AuditDelta.ForDelete(masked);
    }

    private static IReadOnlyDictionary<string, object?> MaskObjectDictionary(
        IReadOnlyDictionary<string, object?> dict,
        IReadOnlySet<string> piiFields)
    {
        return dict.ToDictionary(
            kvp => kvp.Key,
            kvp => piiFields.Contains(kvp.Key) ? (object?)MaskedMarker : kvp.Value);
    }
}
