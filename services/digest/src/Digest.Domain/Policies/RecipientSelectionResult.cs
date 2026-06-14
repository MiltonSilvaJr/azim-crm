namespace Digest.Domain.Policies;

/// <summary>
/// Resultado da avaliação da <see cref="RecipientSelectionPolicy"/>.
/// Indica quais blocos de conteúdo o usuário deve receber.
/// </summary>
/// <param name="ShouldReceivePendencias">Usuário deve receber o digest de pendências.</param>
/// <param name="ShouldReceiveAzimute">Usuário deve receber o azimute semanal.</param>
public sealed record RecipientSelectionResult(bool ShouldReceivePendencias, bool ShouldReceiveAzimute)
{
    /// <summary>Retorna <c>true</c> se o usuário deve receber ao menos um bloco.</summary>
    public bool ShouldReceiveAny => ShouldReceivePendencias || ShouldReceiveAzimute;

    /// <summary>Instância representando "não recebe nada".</summary>
    public static readonly RecipientSelectionResult None = new(false, false);
}
