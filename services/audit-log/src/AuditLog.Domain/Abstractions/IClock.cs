namespace AuditLog.Domain.Abstractions;

/// <summary>
/// Abstração de relógio do servidor para geração de timestamps determinísticos e testáveis.
/// Substitui o uso direto de <c>DateTimeOffset.UtcNow</c> no domínio.
/// </summary>
public interface IClock
{
    /// <summary>Retorna o instante atual no fuso horário UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
