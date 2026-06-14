using AccountManagement.Infrastructure.ReadPorts;
using AccountManagement.Infrastructure.Tests.Persistence;

namespace AccountManagement.Infrastructure.Tests.IdempotencyKey;

/// <summary>
/// Testes de integração para <see cref="IdempotencyKeyRepository"/> com PostgreSQL real.
///
/// Cobre TASK-12 (ST-03):
/// - Chave nova: registra e retorna null (operação deve prosseguir).
/// - Mesma chave + payload igual: retorna responseRef da operação original (idempotência).
/// - Mesma chave + payload divergente: lança IdempotencyKeyConflictException (ACC-ERR-009).
/// - UpdateResponseRefAsync: atualiza a referência de resposta após criação do recurso.
/// - Cross-tenant: chaves de tenants diferentes são isoladas.
///
/// Mapeia: TASK-12 (ST-03), design §6.5, design §7 (idempotency_keys), ACC-ERR-009.
/// </summary>
[Collection("PostgresFixture")]
public sealed class IdempotencyKeyRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public IdempotencyKeyRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // ST-03-a: chave nova — registra e retorna null
    // =========================================================================

    [Fact]
    public async Task CheckAndRegisterAsync_new_key_registers_and_returns_null()
    {
        var tenantId = Guid.NewGuid();
        var key = $"idem-{Guid.NewGuid():N}";

        await using var ctx = _fixture.CreateDbContextNoFilter();
        var repo = new IdempotencyKeyRepository(ctx);

        var result = await repo.CheckAndRegisterAsync(tenantId, key, "hash-abc-123");

        result.Should().BeNull(
            "chave nova deve retornar null para indicar que a operação deve prosseguir");
    }

    // =========================================================================
    // ST-03-b: mesma chave + payload igual — retorna responseRef original
    // =========================================================================

    [Fact]
    public async Task CheckAndRegisterAsync_same_key_same_hash_returns_original_response_ref()
    {
        var tenantId = Guid.NewGuid();
        var key = $"idem-{Guid.NewGuid():N}";
        const string hash = "hash-idempotente-xyz";
        var responseRef = Guid.NewGuid();

        // Primeira chamada: registra a chave
        await using var ctx1 = _fixture.CreateDbContextNoFilter();
        var repo1 = new IdempotencyKeyRepository(ctx1);
        await repo1.CheckAndRegisterAsync(tenantId, key, hash);

        // Atualiza responseRef após criação do recurso
        await repo1.UpdateResponseRefAsync(tenantId, key, responseRef);

        // Segunda chamada com mesmo hash: deve retornar responseRef
        await using var ctx2 = _fixture.CreateDbContextNoFilter();
        var repo2 = new IdempotencyKeyRepository(ctx2);
        var result = await repo2.CheckAndRegisterAsync(tenantId, key, hash);

        result.Should().Be(responseRef,
            "repetição com mesmo payload deve retornar referência da operação original (idempotência)");
    }

    // =========================================================================
    // ST-03-c: mesma chave + payload divergente — lança IdempotencyKeyConflictException
    // =========================================================================

    [Fact]
    public async Task CheckAndRegisterAsync_same_key_different_hash_throws_conflict_exception()
    {
        var tenantId = Guid.NewGuid();
        var key = $"idem-{Guid.NewGuid():N}";
        var responseRef = Guid.NewGuid();

        // Registra a chave com hash original
        await using var ctx1 = _fixture.CreateDbContextNoFilter();
        var repo1 = new IdempotencyKeyRepository(ctx1);
        await repo1.CheckAndRegisterAsync(tenantId, key, "hash-original");
        await repo1.UpdateResponseRefAsync(tenantId, key, responseRef);

        // Segunda chamada com hash divergente
        await using var ctx2 = _fixture.CreateDbContextNoFilter();
        var repo2 = new IdempotencyKeyRepository(ctx2);

        var act = async () => await repo2.CheckAndRegisterAsync(tenantId, key, "hash-DIFERENTE");

        var ex = await act.Should()
            .ThrowAsync<IdempotencyKeyConflictException>(
                "payload divergente na mesma chave deve lançar ACC-ERR-009");

        ex.Which.OriginalResponseRef.Should().Be(responseRef,
            "exceção deve carregar a referência da operação original para retorno ao cliente");
    }

    // =========================================================================
    // ST-03-d: UpdateResponseRefAsync atualiza a referência corretamente
    // =========================================================================

    [Fact]
    public async Task UpdateResponseRefAsync_sets_response_ref_on_existing_key()
    {
        var tenantId = Guid.NewGuid();
        var key = $"idem-{Guid.NewGuid():N}";
        const string hash = "hash-update-test";
        var responseRef = Guid.NewGuid();

        await using var ctx1 = _fixture.CreateDbContextNoFilter();
        var repo1 = new IdempotencyKeyRepository(ctx1);

        // Registra sem responseRef
        var firstResult = await repo1.CheckAndRegisterAsync(tenantId, key, hash);
        firstResult.Should().BeNull("chave nova retorna null");

        // Atualiza responseRef
        await repo1.UpdateResponseRefAsync(tenantId, key, responseRef);

        // Verifica que a segunda chamada retorna o responseRef atualizado
        await using var ctx2 = _fixture.CreateDbContextNoFilter();
        var repo2 = new IdempotencyKeyRepository(ctx2);
        var secondResult = await repo2.CheckAndRegisterAsync(tenantId, key, hash);

        secondResult.Should().Be(responseRef,
            "UpdateResponseRefAsync deve persistir a referência para uso na próxima consulta");
    }

    // =========================================================================
    // ST-03-e: chave null inicialmente, depois preenchida por UpdateResponseRefAsync
    // =========================================================================

    [Fact]
    public async Task CheckAndRegisterAsync_before_update_returns_null_response_ref()
    {
        var tenantId = Guid.NewGuid();
        var key = $"idem-{Guid.NewGuid():N}";
        const string hash = "hash-sem-update";

        // Registra chave sem chamar UpdateResponseRefAsync
        await using var ctx1 = _fixture.CreateDbContextNoFilter();
        var repo1 = new IdempotencyKeyRepository(ctx1);
        await repo1.CheckAndRegisterAsync(tenantId, key, hash);

        // Segunda chamada com mesmo hash — responseRef ainda é null
        await using var ctx2 = _fixture.CreateDbContextNoFilter();
        var repo2 = new IdempotencyKeyRepository(ctx2);
        var result = await repo2.CheckAndRegisterAsync(tenantId, key, hash);

        result.Should().BeNull(
            "responseRef nulo indica que a operação original ainda não concluiu " +
            "ou não produziu referência — cliente deve aguardar");
    }

    // =========================================================================
    // ST-03-f: isolamento cross-tenant — mesma chave em tenants diferentes não conflita
    // =========================================================================

    [Fact]
    public async Task CheckAndRegisterAsync_same_key_different_tenants_are_isolated()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var sharedKey = $"idem-compartilhada-{Guid.NewGuid():N}";
        const string hashA = "hash-tenant-a";
        const string hashB = "hash-tenant-b-diferente";

        // Registra a chave no tenantA
        await using var ctxA = _fixture.CreateDbContextNoFilter();
        var repoA = new IdempotencyKeyRepository(ctxA);
        await repoA.CheckAndRegisterAsync(tenantA, sharedKey, hashA);

        // No tenantB, a mesma chave com hash diferente NÃO deve lançar conflito
        await using var ctxB = _fixture.CreateDbContextNoFilter();
        var repoB = new IdempotencyKeyRepository(ctxB);

        var act = async () => await repoB.CheckAndRegisterAsync(tenantB, sharedKey, hashB);

        await act.Should().NotThrowAsync(
            "chaves de tenants diferentes são isoladas pela PK composta (tenant_id, idempotency_key)");

        var result = await repoB.CheckAndRegisterAsync(tenantB, sharedKey, hashB);
        result.Should().BeNull("tenantB não tem responseRef registrado ainda");
    }

    // =========================================================================
    // ST-03-g: unicidade da PK — não registra chave duplicada para mesmo tenant
    // =========================================================================

    [Fact]
    public async Task CheckAndRegisterAsync_does_not_insert_duplicate_for_same_tenant_and_key()
    {
        var tenantId = Guid.NewGuid();
        var key = $"idem-dedup-{Guid.NewGuid():N}";
        const string hash = "hash-dedup";

        await using var ctx1 = _fixture.CreateDbContextNoFilter();
        var repo1 = new IdempotencyKeyRepository(ctx1);
        await repo1.CheckAndRegisterAsync(tenantId, key, hash);

        // Segunda chamada — deve consultar o existente, não inserir novo
        await using var ctx2 = _fixture.CreateDbContextNoFilter();
        var repo2 = new IdempotencyKeyRepository(ctx2);

        // Não deve lançar exceção de PK duplicada (tratamento é via consulta prévia)
        var act = async () => await repo2.CheckAndRegisterAsync(tenantId, key, hash);
        await act.Should().NotThrowAsync(
            "segunda chamada com mesmo hash não deve tentar inserir duplicata");
    }
}
