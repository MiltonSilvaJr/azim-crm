namespace AuditLog.Domain.ValueObjects;

/// <summary>
/// Par antes/depois de um atributo efetivamente alterado em uma operação <c>update</c>.
/// Imutável; igualdade por valor.
/// </summary>
/// <param name="Before">Valor do atributo antes da operação.</param>
/// <param name="After">Valor do atributo após a operação.</param>
public sealed record AuditAttributeChange(object? Before, object? After);
