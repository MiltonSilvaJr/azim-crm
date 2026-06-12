namespace AuditLog.Domain.Services;

/// <summary>
/// Contrato de política de campos PII (Personally Identifiable Information) por tipo de entidade.
/// Mapeia <c>entity_type</c> para o conjunto de campos que devem ser mascarados antes da persistência.
/// <para>
/// A implementação padrão é <see cref="PiiFieldPolicy"/>. Políticas customizadas podem ser
/// registradas na infraestrutura sem alterar <see cref="PiiMasker"/> (REQ-004.3, design §4.6).
/// </para>
/// </summary>
public interface IPiiFieldPolicy
{
    /// <summary>
    /// Retorna o conjunto de campos PII para o tipo de entidade informado.
    /// Retorna conjunto vazio para entidades sem política registrada.
    /// </summary>
    /// <param name="entityType">Nome do tipo de entidade (ex.: <c>Contact</c>).</param>
    IReadOnlySet<string> GetPiiFields(string entityType);
}
