namespace Organization.Infrastructure.Messaging;

/// <summary>
/// Opções de configuração do <see cref="OutboxWorker"/>.
/// Vinculadas à seção <c>OutboxWorker</c> do <c>appsettings.json</c>.
/// </summary>
public sealed class OutboxWorkerOptions
{
    /// <summary>Nome da seção de configuração.</summary>
    public const string SectionName = "OutboxWorker";

    /// <summary>Intervalo de polling entre ciclos de publicação. Padrão: 5 segundos.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Número máximo de eventos por ciclo de publicação. Padrão: 100.</summary>
    public int BatchSize { get; set; } = 100;
}
