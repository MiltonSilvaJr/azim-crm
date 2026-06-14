namespace PartnerManagement.Application.Ports;

/// <summary>
/// Porta de métricas do módulo partner-management.
/// Declarada no Application; implementada na Infrastructure (<c>PartnerMetrics</c>).
/// Permite que handlers registrem métricas sem depender do Prometheus diretamente.
/// Mapeia: RNF 5, design §11, TASK-26.
/// </summary>
public interface IPartnerMetrics
{
    /// <summary>
    /// Incrementa o contador <c>partners_created_total</c>.
    /// Deve ser chamado após criação bem-sucedida de um parceiro (RNF 5.2).
    /// </summary>
    void RecordPartnerCreated();

    /// <summary>
    /// Incrementa o contador <c>partners_deactivated_total</c>.
    /// Deve ser chamado apenas em transição efetiva Active → Inactive (DD-006).
    /// </summary>
    void RecordPartnerDeactivated();

    /// <summary>
    /// Incrementa o contador <c>partners_reactivated_total</c>.
    /// Deve ser chamado apenas em transição efetiva Inactive → Active (DD-006).
    /// </summary>
    void RecordPartnerReactivated();

    /// <summary>
    /// Registra a duração de uma consulta de visão de comissão no histograma
    /// <c>partner_commission_view_duration_seconds</c>.
    /// </summary>
    /// <param name="duration">Duração da operação.</param>
    void RecordCommissionViewDuration(TimeSpan duration);
}
