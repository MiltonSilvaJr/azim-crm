using AuditLog.Domain.Abstractions;
using AuditLog.Domain.Aggregates;
using AuditLog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AuditLog.Domain.Tests.Aggregates;

/// <summary>
/// Testes de unidade para o aggregate root <see cref="AuditLogAggregate"/>.
/// </summary>
public sealed class AuditLogAggregateTests
{
    // ------------------------------------------------------------------ Doubles

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; }

        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
    }

    // ------------------------------------------------------------------ Helpers

    private static TenantId BuildTenantId() => TenantId.From(Guid.NewGuid());
    private static ActorId BuildActorId() => ActorId.From(Guid.NewGuid());
    private static EntityReference BuildEntityRef(string type = "Opportunity")
        => EntityReference.Create(type, Guid.NewGuid());
    private static AuditDelta BuildDelta()
        => AuditDelta.ForCreate(new Dictionary<string, object?> { ["stage"] = "Prospect" }.AsReadOnly());

    // ------------------------------------------------------------------ Create — caminho feliz

    [Fact(DisplayName = "Create com argumentos válidos cria instância com propriedades corretas")]
    public void Create_ArgumentosValidos_CriaInstancia()
    {
        var tenantId = BuildTenantId();
        var actorId = BuildActorId();
        var entityRef = BuildEntityRef();
        var delta = BuildDelta();
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var log = AuditLogAggregate.Create(tenantId, actorId, entityRef, AuditAction.Create, delta, clock);

        log.TenantId.Should().Be(tenantId);
        log.ActorId.Should().Be(actorId);
        log.EntityReference.Should().Be(entityRef);
        log.Action.Should().Be(AuditAction.Create);
        log.Delta.Should().Be(delta);
    }

    [Fact(DisplayName = "Create gera um Id único e não vazio")]
    public void Create_GeraIdUnico()
    {
        var log1 = AuditLogAggregate.Create(BuildTenantId(), BuildActorId(), BuildEntityRef(),
            AuditAction.Create, BuildDelta(), new FixedClock(DateTimeOffset.UtcNow));
        var log2 = AuditLogAggregate.Create(BuildTenantId(), BuildActorId(), BuildEntityRef(),
            AuditAction.Create, BuildDelta(), new FixedClock(DateTimeOffset.UtcNow));

        log1.Id.Should().NotBe(log2.Id);
        log1.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "Create deriva CreatedAt do IClock, não do chamador")]
    public void Create_CreatedAtVemDoClock()
    {
        var expectedTime = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var clock = new FixedClock(expectedTime);

        var log = AuditLogAggregate.Create(BuildTenantId(), BuildActorId(), BuildEntityRef(),
            AuditAction.Create, BuildDelta(), clock);

        log.CreatedAt.Should().Be(expectedTime);
    }

    [Fact(DisplayName = "Create com relógios diferentes gera CreatedAt diferentes")]
    public void Create_RelogiosDiferentes_CreatedAtDiferente()
    {
        var time1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time2 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var log1 = AuditLogAggregate.Create(BuildTenantId(), BuildActorId(), BuildEntityRef(),
            AuditAction.Create, BuildDelta(), new FixedClock(time1));
        var log2 = AuditLogAggregate.Create(BuildTenantId(), BuildActorId(), BuildEntityRef(),
            AuditAction.Create, BuildDelta(), new FixedClock(time2));

        log1.CreatedAt.Should().NotBe(log2.CreatedAt);
    }

    // ------------------------------------------------------------------ Create — guards

    [Fact(DisplayName = "Create com TenantId nulo lança ArgumentNullException")]
    public void Create_TenantIdNulo_LancaException()
    {
        var act = () => AuditLogAggregate.Create(
            null!, BuildActorId(), BuildEntityRef(), AuditAction.Create, BuildDelta(),
            new FixedClock(DateTimeOffset.UtcNow));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Create com ActorId nulo lança ArgumentNullException")]
    public void Create_ActorIdNulo_LancaException()
    {
        var act = () => AuditLogAggregate.Create(
            BuildTenantId(), null!, BuildEntityRef(), AuditAction.Create, BuildDelta(),
            new FixedClock(DateTimeOffset.UtcNow));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Create com EntityReference nulo lança ArgumentNullException")]
    public void Create_EntityReferenceNulo_LancaException()
    {
        var act = () => AuditLogAggregate.Create(
            BuildTenantId(), BuildActorId(), null!, AuditAction.Create, BuildDelta(),
            new FixedClock(DateTimeOffset.UtcNow));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Create com Delta nulo lança ArgumentNullException")]
    public void Create_DeltaNulo_LancaException()
    {
        var act = () => AuditLogAggregate.Create(
            BuildTenantId(), BuildActorId(), BuildEntityRef(), AuditAction.Create, null!,
            new FixedClock(DateTimeOffset.UtcNow));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Create com IClock nulo lança ArgumentNullException")]
    public void Create_ClockNulo_LancaException()
    {
        var act = () => AuditLogAggregate.Create(
            BuildTenantId(), BuildActorId(), BuildEntityRef(), AuditAction.Create, BuildDelta(),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------ Imutabilidade — sem setters públicos

    [Fact(DisplayName = "AuditLogAggregate não expõe setters públicos em nenhuma propriedade")]
    public void SemSettersPublicos()
    {
        var type = typeof(AuditLogAggregate);

        var publicSetters = type.GetProperties()
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        publicSetters.Should().BeEmpty(
            because: "o aggregate é imutável após criação; nenhuma propriedade deve ter setter público (REQ-002.5)");
    }

    [Fact(DisplayName = "AuditLogAggregate não expõe métodos de mutação (Update/Remove/Modify)")]
    public void SemMetodosDeMutacao()
    {
        var type = typeof(AuditLogAggregate);

        var mutationMethods = type.GetMethods()
            .Where(m => m.IsPublic && !m.IsStatic)
            .Select(m => m.Name)
            .Where(n => n.StartsWith("Update", StringComparison.OrdinalIgnoreCase)
                     || n.StartsWith("Remove", StringComparison.OrdinalIgnoreCase)
                     || n.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
                     || n.StartsWith("Modify", StringComparison.OrdinalIgnoreCase)
                     || n.StartsWith("Change", StringComparison.OrdinalIgnoreCase))
            .ToList();

        mutationMethods.Should().BeEmpty(
            because: "o aggregate é append-only; não deve expor nenhum método de mutação (RNF-001)");
    }

    [Fact(DisplayName = "AuditLogAggregate não possui propriedade UpdatedAt")]
    public void SemPropriedadeUpdatedAt()
    {
        var type = typeof(AuditLogAggregate);

        var hasUpdatedAt = type.GetProperties()
            .Any(p => p.Name.Equals("UpdatedAt", StringComparison.OrdinalIgnoreCase));

        hasUpdatedAt.Should().BeFalse(
            because: "registros de auditoria não têm semântica de atualização (REQ-002.5)");
    }

    // ------------------------------------------------------------------ Reconstitute

    [Fact(DisplayName = "Reconstitute preserva todos os campos fornecidos")]
    public void Reconstitute_PreservaTodosOsCampos()
    {
        var id = AuditLogId.New();
        var tenantId = BuildTenantId();
        var actorId = BuildActorId();
        var entityRef = BuildEntityRef();
        var delta = BuildDelta();
        var createdAt = new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero);

        var log = AuditLogAggregate.Reconstitute(id, tenantId, actorId, entityRef,
            AuditAction.Update, delta, createdAt);

        log.Id.Should().Be(id);
        log.TenantId.Should().Be(tenantId);
        log.ActorId.Should().Be(actorId);
        log.EntityReference.Should().Be(entityRef);
        log.Action.Should().Be(AuditAction.Update);
        log.Delta.Should().Be(delta);
        log.CreatedAt.Should().Be(createdAt);
    }

    [Fact(DisplayName = "Reconstitute com Id nulo lança ArgumentNullException")]
    public void Reconstitute_IdNulo_LancaException()
    {
        var act = () => AuditLogAggregate.Reconstitute(
            null!, BuildTenantId(), BuildActorId(), BuildEntityRef(),
            AuditAction.Create, BuildDelta(), DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Reconstitute com TenantId nulo lança ArgumentNullException")]
    public void Reconstitute_TenantIdNulo_LancaException()
    {
        var act = () => AuditLogAggregate.Reconstitute(
            AuditLogId.New(), null!, BuildActorId(), BuildEntityRef(),
            AuditAction.Create, BuildDelta(), DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Reconstitute com Delta nulo lança ArgumentNullException")]
    public void Reconstitute_DeltaNulo_LancaException()
    {
        var act = () => AuditLogAggregate.Reconstitute(
            AuditLogId.New(), BuildTenantId(), BuildActorId(), BuildEntityRef(),
            AuditAction.Create, null!, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }
}
