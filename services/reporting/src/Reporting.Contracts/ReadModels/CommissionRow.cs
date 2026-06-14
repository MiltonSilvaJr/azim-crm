namespace Reporting.Contracts.ReadModels;

/// <summary>
/// Linha bruta do relatório de comissões por parceiro, retornada pelo repositório.
/// O handler separa linhas por <see cref="IsSnapshot"/> para calcular consolidado (PBT-01, RN-007).
/// Valores monetários em centavos inteiros (DD-007).
/// Mapeia: Req 4, design §5.2, TASK-10.
/// </summary>
public sealed record CommissionRow(
    Guid PartnerId,
    string PartnerName,
    long CommissionCents,
    bool IsSnapshot,
    string StageCategory);
