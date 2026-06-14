using DataMigration.Domain.Aggregates;
using DataMigration.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DataMigration.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de integração do <see cref="SharedUnitOfWork"/> com PostgreSQL real.
///
/// Valida DD-001: import tudo-ou-nada em transação única.
/// - Commit normal persiste os dados.
/// - Transação rollback-only (dry-run) não persiste dados.
/// - Rollback explícito desfaz as escritas.
///
/// Rastreia: TASK-18, DD-001, Req 6, RNF 5.
/// </summary>
[Collection("PostgresContainer")]
public sealed class SharedUnitOfWorkTests
{
    private readonly PostgresContainerFixture _fixture;

    public SharedUnitOfWorkTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // Commit normal
    // =========================================================================

    [Fact]
    public async Task CommitAsync_TransacaoNormal_PersisteDados()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);

        var job = CreateJob(tenantId);
        await ctx.MigrationJobs.AddAsync(job);

        // Act — transação normal: commit.
        await uow.BeginAsync();
        await uow.CommitAsync();

        // Assert — dados persistidos.
        await using var ctxRead = _fixture.CreateContext(tenantId);
        var found = await ctxRead.MigrationJobs.FindAsync(job.Id);
        found.Should().NotBeNull("commit normal deve persistir os dados (DD-001)");
    }

    // =========================================================================
    // Rollback-only (dry-run)
    // =========================================================================

    [Fact]
    public async Task CommitAsync_TransacaoRollbackOnly_NaoPersisteDados()
    {
        // Arrange — dry-run: transação rollback-only.
        var tenantId = Guid.NewGuid();
        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);

        var job = CreateJob(tenantId);
        await ctx.MigrationJobs.AddAsync(job);

        // Act — dry-run: commit chama RollbackAsync internamente.
        await uow.BeginRollbackOnlyAsync();
        await uow.CommitAsync(); // deve rolar back silenciosamente.

        // Assert — dados não persistidos (dry-run, Req 2.1).
        await using var ctxRead = _fixture.CreateContext(tenantId);
        var found = await ctxRead.MigrationJobs.FindAsync(job.Id);
        found.Should().BeNull("rollback-only não deve persistir nada (dry-run, DD-001)");
    }

    [Fact]
    public async Task IsRollbackOnly_QuandoBeginRollbackOnly_ReturnsTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);

        // Assert inicial
        uow.IsRollbackOnly.Should().BeFalse("antes de begin, não é rollback-only");

        // Act
        await uow.BeginRollbackOnlyAsync();

        // Assert
        uow.IsRollbackOnly.Should().BeTrue("após BeginRollbackOnly, deve ser rollback-only");

        // Cleanup
        await uow.RollbackAsync();
    }

    // =========================================================================
    // Rollback explícito
    // =========================================================================

    [Fact]
    public async Task RollbackAsync_DesfazEscritas()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);

        var job = CreateJob(tenantId);
        await ctx.MigrationJobs.AddAsync(job);

        // Act — begin e depois rollback sem commit.
        await uow.BeginAsync();
        await ctx.SaveChangesAsync();  // salva dentro da transação.
        await uow.RollbackAsync();     // reverte tudo.

        // Assert — dados revertidos.
        await using var ctxRead = _fixture.CreateContext(tenantId);
        var found = await ctxRead.MigrationJobs.FindAsync(job.Id);
        found.Should().BeNull("rollback deve desfazer todas as escritas (DD-001)");
    }

    [Fact]
    public async Task BeginAsync_TransacaoJaIniciada_LancaInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = _fixture.CreateContext(tenantId);
        var uow = new SharedUnitOfWork(ctx);

        await uow.BeginAsync();

        // Act
        var act = async () => await uow.BeginAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            "não pode iniciar transação dentro de outra transação");

        // Cleanup
        await uow.RollbackAsync();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static MigrationJob CreateJob(Guid tenantId) =>
        MigrationJob.Create(
            tenantId: tenantId,
            sourceFileName: "uow-test.xlsx",
            sourceFileSizeBytes: 512,
            sourceFileHash: Guid.NewGuid().ToString("N"),
            detectedRowCount: 5,
            createdBy: Guid.NewGuid());
}
