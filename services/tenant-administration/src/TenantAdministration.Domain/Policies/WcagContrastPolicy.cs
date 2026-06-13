using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Domain.Policies;

/// <summary>
/// Verifica conformidade com WCAG 2.1 AA para texto normal (razão ≥ 4,5:1).
/// Comparação determinística via <c>round(R, 2) >= 4.50</c> para evitar instabilidade
/// de ponto flutuante nos limítrofes (DD-003).
/// Sem estado, sem I/O, sem clock — função pura.
/// PBT-04 valida: aprovação ⟺ round(R,2) ≥ 4.50 para qualquer par gerado.
/// </summary>
public static class WcagContrastPolicy
{
    private const decimal ApprovalThreshold = 4.50m;

    /// <summary>
    /// Calcula a razão de contraste e determina aprovação WCAG AA para texto normal.
    /// </summary>
    /// <param name="colors">Par de cores primária/secundária a avaliar.</param>
    /// <returns>
    /// Tupla com <c>approved</c> (bool) e <c>ratio</c> (decimal com 2 casas).
    /// </returns>
    public static (bool Approved, decimal Ratio) Check(ColorPair colors)
    {
        var l1 = ColorPair.RelativeLuminance(colors.Primary);
        var l2 = ColorPair.RelativeLuminance(colors.Secondary);

        // Garante que L1 >= L2 conforme a fórmula WCAG
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);

        var rawRatio = (lighter + 0.05) / (darker + 0.05);
        var ratio = Math.Round((decimal)rawRatio, 2);

        return (ratio >= ApprovalThreshold, ratio);
    }
}
