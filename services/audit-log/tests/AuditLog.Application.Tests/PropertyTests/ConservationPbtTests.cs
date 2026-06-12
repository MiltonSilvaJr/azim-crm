using AuditLog.Application.Abstractions;
using AuditLog.Application.Commands;
using AuditLog.Application.Handlers;
using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.Repositories;
using AuditLog.Domain.Services;
using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AuditLog.Application.Tests.PropertyTests;

/// <summary>
/// PBT-02 — Conservação: o número de registros persistidos deve ser igual ao número de
/// <see cref="RecordAuditEntryCommand"/> executados com sucesso; cada registro deve ter
/// <c>user_id</c> não vazio e <c>delta_json</c> (delta) coerente com a operação.
/// <para>
/// Referência: design §13.1, PBT-02, requirements PBT-02.
/// Gerador: sequências de N comandos Create aleatórios (N ∈ [1..50]).
/// </para>
/// </summary>
public sealed class ConservationPbtTests
{
    // -----------------------------------------------------------------------
    // Repositório in-memory como spy
    // -----------------------------------------------------------------------

    /// <summary>
    /// Repositório in-memory que captura todos os AddAsync para inspeção nos testes de propriedade.
    /// Implementado como classe de suporte — sem dependência de infraestrutura.
    /// </summary>
    private sealed class SpyAuditLogRepository : IAuditLogRepository
    {
        private readonly List<AuditLogAggregate> _entries = new();

        public IReadOnlyList<AuditLogAggregate> Entries => _entries.AsReadOnly();

        public void Reset() => _entries.Clear();

        public Task AddAsync(AuditLogAggregate auditLog, CancellationToken cancellationToken = default)
        {
            _entries.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditLogAggregate>> FindByEntityAsync(
            TenantId tenantId,
            EntityReference entityReference,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLogAggregate>>(_entries
                .Where(e => e.EntityReference.EntityId == entityReference.EntityId)
                .ToList());

        public Task<(IReadOnlyList<AuditLogAggregate> Items, int TotalCount)> ListAsync(
            TenantId tenantId,
            string? entityType = null,
            Guid? entityId = null,
            Guid? actorId = null,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            var result = _entries.ToList();
            return Task.FromResult<(IReadOnlyList<AuditLogAggregate>, int)>((result, result.Count));
        }
    }

    // -----------------------------------------------------------------------
    // Geradores FsCheck
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gera um <see cref="RecordAuditEntryCommand"/> de criação com campos aleatórios válidos.
    /// </summary>
    private static Gen<RecordAuditEntryCommand> CommandGen()
    {
        return from actorId in Arb.Generate<Guid>().Where(g => g != Guid.Empty)
               from entityId in Arb.Generate<Guid>().Where(g => g != Guid.Empty)
               from entityTypeSuffix in Gen.Choose(1, 40).Select(n => new string('E', n))
               select new RecordAuditEntryCommand
               {
                   ActorId = actorId,
                   EntityType = "Entity" + entityTypeSuffix.Substring(0, Math.Min(entityTypeSuffix.Length, 43)),
                   EntityId = entityId,
                   Action = AuditAction.Create,
                   RawBefore = null,
                   RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal)
                   {
                       ["field1"] = "value1",
                       ["count"] = 42L // long = centavos inteiros (DD-005)
                   }
               };
    }

    /// <summary>
    /// Gera uma sequência de N comandos (N ∈ [1..50]).
    /// </summary>
    private static Gen<List<RecordAuditEntryCommand>> CommandListGen() =>
        Gen.Choose(1, 50).SelectMany(n =>
            Gen.Sequence(Enumerable.Repeat(CommandGen(), n))
               .Select(seq => seq.ToList()));

    // -----------------------------------------------------------------------
    // PBT-02: Count == N
    // -----------------------------------------------------------------------

    [Property(MaxTest = 200, DisplayName = "PBT-02: número de registros persistidos deve ser igual ao número de comandos")]
    public Property PBT02_Conservation_Count_Equals_N_Commands()
    {
        return Prop.ForAll(
            Arb.From(CommandListGen()),
            commands =>
            {
                // Setup: spy limpo a cada execução do gerador
                var spy = new SpyAuditLogRepository();
                var (handler, _) = BuildHandler(spy);

                // Act: executa todos os comandos
                foreach (var cmd in commands)
                    handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();

                // Assert: Count deve ser igual ao número de comandos enviados
                return spy.Entries.Count == commands.Count;
            });
    }

    // -----------------------------------------------------------------------
    // PBT-02: user_id não vazio em todos os registros
    // -----------------------------------------------------------------------

    [Property(MaxTest = 200, DisplayName = "PBT-02: todos os registros persistidos devem ter user_id não vazio")]
    public Property PBT02_All_Entries_Have_NonEmpty_UserId()
    {
        return Prop.ForAll(
            Arb.From(CommandListGen()),
            commands =>
            {
                var spy = new SpyAuditLogRepository();
                var (handler, _) = BuildHandler(spy);

                foreach (var cmd in commands)
                    handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();

                return spy.Entries.All(e => e.ActorId.Value != Guid.Empty);
            });
    }

    // -----------------------------------------------------------------------
    // PBT-02: delta não vazio e coerente com a operação Create
    // -----------------------------------------------------------------------

    [Property(MaxTest = 200, DisplayName = "PBT-02: delta de cada registro deve ser não vazio e coerente com Create")]
    public Property PBT02_All_Entries_Have_NonEmpty_Delta_Consistent_With_Create()
    {
        return Prop.ForAll(
            Arb.From(CommandListGen()),
            commands =>
            {
                var spy = new SpyAuditLogRepository();
                var (handler, _) = BuildHandler(spy);

                foreach (var cmd in commands)
                    handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();

                return spy.Entries.All(e =>
                    e.Delta.Kind == AuditDeltaKind.Create &&
                    e.Delta.After != null &&
                    e.Delta.After.Count > 0);
            });
    }

    // -----------------------------------------------------------------------
    // PBT-02: sem duplicação (IDs únicos)
    // -----------------------------------------------------------------------

    [Property(MaxTest = 200, DisplayName = "PBT-02: nenhum registro deve ser duplicado (IDs de auditoria únicos)")]
    public Property PBT02_No_Duplicate_Entries()
    {
        return Prop.ForAll(
            Arb.From(CommandListGen()),
            commands =>
            {
                var spy = new SpyAuditLogRepository();
                var (handler, _) = BuildHandler(spy);

                foreach (var cmd in commands)
                    handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();

                var distinctIds = spy.Entries.Select(e => e.Id.Value).Distinct().Count();
                return distinctIds == spy.Entries.Count;
            });
    }

    // -----------------------------------------------------------------------
    // Testes determinísticos complementares (xUnit)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "PBT-02 smoke: 1 comando deve gerar exatamente 1 registro")]
    public async Task PBT02_Single_Command_Generates_One_Entry()
    {
        var spy = new SpyAuditLogRepository();
        var (handler, _) = BuildHandler(spy);

        var cmd = new RecordAuditEntryCommand
        {
            ActorId = Guid.NewGuid(),
            EntityType = "Opportunity",
            EntityId = Guid.NewGuid(),
            Action = AuditAction.Create,
            RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" }
        };

        await handler.Handle(cmd, CancellationToken.None);

        spy.Entries.Should().HaveCount(1);
        spy.Entries[0].ActorId.Value.Should().Be(cmd.ActorId);
        spy.Entries[0].Delta.Kind.Should().Be(AuditDeltaKind.Create);
        spy.Entries[0].Delta.After.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "PBT-02 smoke: spy deve ser limpo entre execuções do gerador")]
    public async Task PBT02_Spy_Resets_Between_Runs()
    {
        var spy = new SpyAuditLogRepository();
        var (handler, _) = BuildHandler(spy);

        // 1ª rodada: 3 comandos
        for (var i = 0; i < 3; i++)
            await handler.Handle(NewCommand(), CancellationToken.None);

        spy.Entries.Should().HaveCount(3);

        // Reset entre rodadas
        spy.Reset();

        // 2ª rodada: 2 comandos
        for (var i = 0; i < 2; i++)
            await handler.Handle(NewCommand(), CancellationToken.None);

        spy.Entries.Should().HaveCount(2, because: "spy foi limpo entre rodadas");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (AuditService handler, ITenantContext tenantContext) BuildHandler(
        IAuditLogRepository repository)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(Guid.NewGuid());

        var metrics = Substitute.For<IAuditMetrics>();

        var policy = Substitute.For<IPiiFieldPolicy>();
        policy.GetPiiFields(Arg.Any<string>())
              .Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var handler = new AuditService(
            repository,
            new PiiMasker(policy),
            policy,
            clock,
            tenantContext,
            metrics,
            NullLogger<AuditService>.Instance);

        return (handler, tenantContext);
    }

    private static RecordAuditEntryCommand NewCommand() => new()
    {
        ActorId = Guid.NewGuid(),
        EntityType = "Opportunity",
        EntityId = Guid.NewGuid(),
        Action = AuditAction.Create,
        RawAfter = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Test" }
    };
}
