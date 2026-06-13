using TenantAdministration.Domain.ValueObjects;

namespace TenantAdministration.Domain.Policies;

/// <summary>
/// Função pura que deriva variações tonais (hover, active, muted) a partir de um
/// <see cref="ColorPair"/> via manipulação determinística no espaço HSL.
/// Sem aleatoriedade, sem clock, sem I/O.
/// PBT-05 valida: <c>Derive(c) == Derive(c)</c> para qualquer entrada.
/// </summary>
public static class ToneDerivationService
{
    // Ajustes fixos de lightness no espaço HSL
    private const double HoverLightnessAdjust = -0.08;    // mais escuro para hover
    private const double ActiveLightnessAdjust = -0.15;   // ainda mais escuro para active
    private const double MutedLightnessAdjust = +0.30;    // mais claro para muted

    /// <summary>
    /// Deriva tons HSL determinísticos a partir do par de cores fornecido.
    /// </summary>
    /// <param name="colors">Par de cores base.</param>
    /// <returns>Tons derivados como <see cref="DerivedTones"/> imutável.</returns>
    public static DerivedTones Derive(ColorPair colors)
    {
        var (ph, ps, pl) = HexToHsl(colors.Primary);
        var (sh, ss, sl) = HexToHsl(colors.Secondary);

        return new DerivedTones(
            PrimaryHover: HslToHex(ph, ps, Clamp01(pl + HoverLightnessAdjust)),
            PrimaryActive: HslToHex(ph, ps, Clamp01(pl + ActiveLightnessAdjust)),
            PrimaryMuted: HslToHex(ph, ps, Clamp01(pl + MutedLightnessAdjust)),
            SecondaryHover: HslToHex(sh, ss, Clamp01(sl + HoverLightnessAdjust)),
            SecondaryActive: HslToHex(sh, ss, Clamp01(sl + ActiveLightnessAdjust)),
            SecondaryMuted: HslToHex(sh, ss, Clamp01(sl + MutedLightnessAdjust))
        );
    }

    // ──────────────────────────────────────────────
    // Funções privadas de conversão de cor
    // ──────────────────────────────────────────────

    /// <summary>Converte cor hex <c>#RRGGBB</c> para HSL (h: 0..360, s,l: 0..1).</summary>
    private static (double H, double S, double L) HexToHsl(string hex)
    {
        var r = Convert.ToInt32(hex[1..3], 16) / 255.0;
        var g = Convert.ToInt32(hex[3..5], 16) / 255.0;
        var b = Convert.ToInt32(hex[5..7], 16) / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;
        double h, s;

        if (Math.Abs(max - min) < 1e-10)
        {
            h = 0;
            s = 0;
        }
        else
        {
            var d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == r)
                h = ((g - b) / d + (g < b ? 6 : 0)) / 6.0;
            else if (max == g)
                h = ((b - r) / d + 2) / 6.0;
            else
                h = ((r - g) / d + 4) / 6.0;

            h *= 360;
        }

        return (h, s, l);
    }

    /// <summary>Converte HSL (h: 0..360, s,l: 0..1) para hex <c>#RRGGBB</c>.</summary>
    private static string HslToHex(double h, double s, double l)
    {
        double r, g, b;

        if (s < 1e-10)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            var hNorm = h / 360.0;
            r = HueToRgb(p, q, hNorm + 1.0 / 3);
            g = HueToRgb(p, q, hNorm);
            b = HueToRgb(p, q, hNorm - 1.0 / 3);
        }

        var ri = (int)Math.Round(r * 255);
        var gi = (int)Math.Round(g * 255);
        var bi = (int)Math.Round(b * 255);

        return $"#{Clamp255(ri):X2}{Clamp255(gi):X2}{Clamp255(bi):X2}";
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    private static double Clamp01(double value) => Math.Max(0.0, Math.Min(1.0, value));

    private static int Clamp255(int value) => Math.Max(0, Math.Min(255, value));
}
