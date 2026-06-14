namespace Organization.Application.Ports;

/// <summary>
/// Port para emissão de alertas operacionais relacionados ao último Tenant Admin.
/// Implementado via log estruturado na camada de Infrastructure (RNF 6.3, MSG-017, design §11).
/// Sem PII nos parâmetros: apenas identificadores opacos.
/// </summary>
public interface ILastTenantAdminAlertService
{
    /// <summary>
    /// Emite alerta operacional quando o tenant está prestes a ficar com um único TAdmin ativo.
    /// Deve ser chamado quando <c>count(TAdmin ativo) == 1</c> e a operação em questão
    /// não o remove (operação permitida, mas estado crítico detectado).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant em estado de alerta.</param>
    /// <param name="remainingAdminCount">Contagem atual de TAdmins ativos (deve ser 1 neste caso).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task AlertLastAdminAsync(Guid tenantId, int remainingAdminCount, CancellationToken cancellationToken = default);
}
