namespace PartnerManagement.Application.Ports;

/// <summary>
/// Porta de abstração do relógio do sistema.
/// Permite injetar tempo controlado em testes sem depender de <see cref="DateTimeOffset.UtcNow"/> direto.
/// Implementada pela Infrastructure ou injeção de teste.
/// Mapeia: design §5.4, RNF 5.
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante atual em UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
