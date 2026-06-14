using Organization.Domain.Events;
using Organization.Domain.Exceptions;
using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

/// <summary>
/// Aggregate root de Business Unit.
/// Encapsula as entidades <see cref="Stage"/>, <see cref="OriginChannel"/> e <see cref="LossReason"/>
/// cujas invariantes pertencem à consistência da BU (DD-006).
///
/// Invariantes protegidas:
/// - Nome único por tenant (verificado via índice e <c>BusinessUnitNameUniquenessSpec</c>).
/// - Exatamente um estágio <c>won</c> e um <c>lost</c>; ao menos um <c>open</c> (<see cref="Policies.TerminalStagesPolicy"/>).
/// - Posições de estágio distintas e ordem total (<see cref="Policies.StagePositionPolicy"/>).
/// - Ao menos um motivo de perda ativo para operar (<see cref="Policies.BusinessUnitEnablementSpec"/>).
/// - Soft-delete: transição <c>active = true → false</c> é monotônica no MVP.
/// </summary>
public sealed class BusinessUnit : AggregateRoot
{
    private readonly List<Stage> _stages = [];
    private readonly List<OriginChannel> _originChannels = [];
    private readonly List<LossReason> _lossReasons = [];

    /// <summary>Identificador único da BU.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador do tenant ao qual a BU pertence.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Nome da BU (objeto de valor).</summary>
    public BusinessUnitName Name { get; private set; } = null!;

    /// <summary>Indica se a BU está ativa.</summary>
    public bool Active { get; private set; }

    /// <summary>Momento de criação da BU.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Momento de desativação (preenchido quando <see cref="Active"/> = false).</summary>
    public DateTimeOffset? DeactivatedAt { get; private set; }

    /// <summary>Estágios do pipeline da BU (somente leitura).</summary>
    public IReadOnlyList<Stage> Stages => _stages.AsReadOnly();

    /// <summary>Canais de origem da BU (somente leitura).</summary>
    public IReadOnlyList<OriginChannel> OriginChannels => _originChannels.AsReadOnly();

    /// <summary>Motivos de perda da BU (somente leitura).</summary>
    public IReadOnlyList<LossReason> LossReasons => _lossReasons.AsReadOnly();

    private BusinessUnit() { }

    /// <summary>
    /// Cria uma nova Business Unit ativa e emite <see cref="BusinessUnitCreated"/>.
    /// </summary>
    /// <param name="name">Nome validado da BU.</param>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="now">Instante de criação (fornecido pela Application, nunca pelo domínio interno).</param>
    public static BusinessUnit Create(BusinessUnitName name, Guid tenantId, DateTimeOffset now)
    {
        var bu = new BusinessUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Active = true,
            CreatedAt = now,
        };

        bu.RaiseDomainEvent(new BusinessUnitCreated(tenantId, bu.Id, name.Value, now));
        return bu;
    }

    /// <summary>
    /// Renomeia a BU.
    /// </summary>
    /// <param name="newName">Novo nome validado.</param>
    /// <exception cref="DomainException">Quando a BU está inativa.</exception>
    public void Rename(BusinessUnitName newName)
    {
        EnsureActive();
        Name = newName;
    }

    /// <summary>
    /// Desativa a BU (soft-delete). Emite <see cref="BusinessUnitDeactivated"/>.
    /// </summary>
    /// <param name="now">Instante de desativação.</param>
    /// <exception cref="DomainException">Quando a BU já está inativa.</exception>
    public void Deactivate(DateTimeOffset now)
    {
        if (!Active)
            throw new DomainException("ORG-ERR-002", "A Business Unit já está inativa.");

        Active = false;
        DeactivatedAt = now;
        RaiseDomainEvent(new BusinessUnitDeactivated(TenantId, Id, now));
    }

    // ── Stage ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adiciona um estágio ao pipeline da BU.
    /// Valida unicidade de nome (case-insensitive) e unicidade de position.
    /// Emite <see cref="StageConfigured"/>.
    /// </summary>
    /// <param name="name">Nome do estágio.</param>
    /// <param name="probability">Probabilidade de fechamento.</param>
    /// <param name="category">Categoria (open, won, lost).</param>
    /// <param name="position">Posição na ordenação do pipeline.</param>
    /// <param name="stageId">Identificador pré-gerado (permite controle pelo Application).</param>
    /// <exception cref="DomainException">
    ///   ORG-ERR-013 quando nome duplicado; ORG-ERR-014 quando position duplicada.
    /// </exception>
    public void AddStage(string name, Probability probability, StageCategory category, int position, Guid stageId)
    {
        EnsureActive();
        EnsureUniqueStageNameOnAdd(name);
        EnsureUniquePositionOnAdd(position);

        var stage = new Stage(stageId, name, probability, category, position);
        _stages.Add(stage);
        RaiseDomainEvent(new StageConfigured(TenantId, Id, stageId, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Remove um estágio do pipeline, respeitando a <c>TerminalStagesPolicy</c>.
    /// </summary>
    /// <param name="stageId">Identificador do estágio a remover.</param>
    /// <exception cref="DomainException">
    ///   ORG-ERR-015 quando a remoção violaria a política de terminais.
    /// </exception>
    public void RemoveStage(Guid stageId)
    {
        EnsureActive();
        var stage = _stages.FirstOrDefault(s => s.Id == stageId)
            ?? throw new DomainException("ORG-ERR-016", $"Estágio '{stageId}' não encontrado na BU.");

        EnforceTerminalStagePolicyOnRemove(stage);
        _stages.Remove(stage);
    }

    /// <summary>
    /// Reordena estágios conforme o mapa informado.
    /// Valida que as novas posições são únicas.
    /// </summary>
    /// <param name="newPositions">Mapa de stageId → nova position.</param>
    /// <exception cref="DomainException">ORG-ERR-014 quando há positions duplicadas.</exception>
    public void ReorderStages(IReadOnlyDictionary<Guid, int> newPositions)
    {
        EnsureActive();

        // Calcula as posições resultantes após aplicar o mapa.
        // Para stages no mapa: usa nova posição.
        // Para stages fora do mapa: mantém posição atual.
        // Valida que o resultado final não tem duplicatas.
        var resultingPositions = _stages.Select(s =>
            newPositions.TryGetValue(s.Id, out var newPos) ? newPos : s.Position).ToList();

        if (resultingPositions.Count != resultingPositions.Distinct().Count())
            throw new DomainException("ORG-ERR-014", "As posições de estágio devem ser únicas após a reordenação. ORG-ERR-014");

        foreach (var (stageId, newPos) in newPositions)
        {
            var stage = _stages.FirstOrDefault(s => s.Id == stageId);
            stage?.UpdatePosition(newPos);
        }
    }

    // ── LossReason ───────────────────────────────────────────────────────────

    /// <summary>
    /// Adiciona um motivo de perda à BU.
    /// </summary>
    /// <param name="name">Nome do motivo.</param>
    /// <param name="reasonId">Identificador pré-gerado.</param>
    public void AddLossReason(string name, Guid reasonId)
    {
        EnsureActive();
        _lossReasons.Add(new LossReason(reasonId, name));
    }

    /// <summary>
    /// Desativa um motivo de perda, respeitando a <c>BusinessUnitEnablementSpec</c>.
    /// </summary>
    /// <param name="reasonId">Identificador do motivo a desativar.</param>
    /// <exception cref="DomainException">ORG-ERR-017 quando é o último motivo ativo.</exception>
    public void DeactivateLossReason(Guid reasonId)
    {
        EnsureActive();
        var reason = _lossReasons.FirstOrDefault(r => r.Id == reasonId)
            ?? throw new DomainException("ORG-ERR-016", $"Motivo de perda '{reasonId}' não encontrado.");

        var activeCount = _lossReasons.Count(r => r.Active);
        if (activeCount <= 1)
            throw new DomainException(
                "ORG-ERR-017",
                "A Business Unit precisa de ao menos um motivo de perda ativo. ORG-ERR-017");

        reason.Deactivate();
    }

    // ── OriginChannel ─────────────────────────────────────────────────────────

    /// <summary>Adiciona um canal de origem à BU.</summary>
    public void AddOriginChannel(string name, Guid channelId)
    {
        EnsureActive();
        _originChannels.Add(new OriginChannel(channelId, name));
    }

    /// <summary>Desativa um canal de origem.</summary>
    public void DeactivateOriginChannel(Guid channelId)
    {
        EnsureActive();
        var channel = _originChannels.FirstOrDefault(c => c.Id == channelId)
            ?? throw new DomainException("ORG-ERR-016", $"Canal de origem '{channelId}' não encontrado.");
        channel.Deactivate();
    }

    // ── Invariantes privadas ──────────────────────────────────────────────────

    private void EnsureActive()
    {
        if (!Active)
            throw new DomainException("ORG-ERR-002", "Não é possível operar em uma Business Unit inativa.");
    }

    private void EnsureUniqueStageNameOnAdd(string name)
    {
        if (_stages.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException(
                "ORG-ERR-013",
                $"Já existe um estágio com o nome '{name}' nesta Business Unit. ORG-ERR-013");
    }

    private void EnsureUniquePositionOnAdd(int position)
    {
        if (_stages.Any(s => s.Position == position))
            throw new DomainException(
                "ORG-ERR-014",
                $"Já existe um estágio na posição {position} desta Business Unit. ORG-ERR-014");
    }

    private void EnforceTerminalStagePolicyOnRemove(Stage stage)
    {
        if (stage.Category.IsTerminal)
        {
            // Conserva exatamente 1 won e 1 lost (TerminalStagesPolicy - PBT-06)
            var terminalCountAfterRemoval = _stages.Count(s => s.Category == stage.Category) - 1;
            if (terminalCountAfterRemoval < 1)
                throw new DomainException(
                    "ORG-ERR-015",
                    $"Não é possível remover o último estágio terminal ({stage.Category.Value}). ORG-ERR-015");
        }
        else
        {
            // Conserva ao menos 1 open (TerminalStagesPolicy - PBT-06)
            var openCountAfterRemoval = _stages.Count(s => s.Category == StageCategory.Open) - 1;
            if (openCountAfterRemoval < 1)
                throw new DomainException(
                    "ORG-ERR-015",
                    "Não é possível remover o último estágio aberto (open). ORG-ERR-015");
        }
    }
}
