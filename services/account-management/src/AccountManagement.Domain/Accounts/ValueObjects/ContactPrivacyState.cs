namespace AccountManagement.Domain.Accounts.ValueObjects;

/// <summary>
/// Objeto de valor que representa o estado de privacidade de um contato.
///
/// Estados possíveis:
/// - <see cref="Active"/> — contato ativo com PII disponível.
/// - <see cref="Anonymized"/> — PII removida por direito ao esquecimento (LGPD).
///
/// A transição <c>Active → Anonymized</c> é monotônica e irreversível
/// (PBT-03, Req 7.5, DD-001). Não há estado <c>Deleted</c> no MVP.
///
/// Mapeia: design §4.3, design §4.5, Req 7.5, PBT-03.
/// </summary>
public sealed class ContactPrivacyState : IEquatable<ContactPrivacyState>
{
    // =========================================================================
    // Instâncias singleton dos estados canônicos
    // =========================================================================

    /// <summary>Contato ativo — PII disponível sob RBAC.</summary>
    public static readonly ContactPrivacyState Active = new(PrivacyStateValue.Active);

    /// <summary>
    /// Contato anonimizado — PII substituída por marcadores; transição irreversível.
    /// O <c>contact_id</c> é preservado para integridade referencial (Req 7.3, PBT-03).
    /// </summary>
    public static readonly ContactPrivacyState Anonymized = new(PrivacyStateValue.Anonymized);

    // =========================================================================
    // Estado interno
    // =========================================================================

    private readonly PrivacyStateValue _value;

    private ContactPrivacyState(PrivacyStateValue value) => _value = value;

    // =========================================================================
    // Propriedades de conveniência
    // =========================================================================

    /// <summary>Retorna <c>true</c> quando o contato está ativo.</summary>
    public bool IsActive => _value == PrivacyStateValue.Active;

    /// <summary>Retorna <c>true</c> quando o contato foi anonimizado.</summary>
    public bool IsAnonymized => _value == PrivacyStateValue.Anonymized;

    /// <summary>Representação em string para persistência (<c>active</c> ou <c>anonymized</c>).</summary>
    public string PersistenceValue => _value switch
    {
        PrivacyStateValue.Active => "active",
        PrivacyStateValue.Anonymized => "anonymized",
        _ => throw new InvalidOperationException($"Estado de privacidade desconhecido: {_value}")
    };

    // =========================================================================
    // Factory para reconstituição a partir de persistência
    // =========================================================================

    /// <summary>
    /// Reconstitui um <see cref="ContactPrivacyState"/> a partir do valor persistido.
    /// </summary>
    /// <param name="value">Valor persistido (<c>active</c> ou <c>anonymized</c>).</param>
    /// <returns>Estado correspondente.</returns>
    /// <exception cref="ArgumentException">Valor desconhecido.</exception>
    public static ContactPrivacyState FromPersistenceValue(string value) =>
        value?.ToLowerInvariant() switch
        {
            "active" => Active,
            "anonymized" => Anonymized,
            _ => throw new ArgumentException($"Estado de privacidade desconhecido: '{value}'.", nameof(value))
        };

    // =========================================================================
    // Igualdade por valor
    // =========================================================================

    /// <inheritdoc />
    public bool Equals(ContactPrivacyState? other) =>
        other is not null && _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is ContactPrivacyState other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => PersistenceValue;

    // =========================================================================
    // Enum privado de estados
    // =========================================================================

    private enum PrivacyStateValue
    {
        Active,
        Anonymized
    }
}
