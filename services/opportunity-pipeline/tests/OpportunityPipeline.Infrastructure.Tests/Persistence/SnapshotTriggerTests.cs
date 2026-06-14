using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace OpportunityPipeline.Infrastructure.Tests.Persistence;

/// <summary>
/// Testes de imutabilidade do snapshot de comissão no banco.
/// Verifica que o trigger trg_block_snapshot_mutation impede UPDATE/DELETE
/// em registros com is_snapshot = TRUE (RNF 5, DD-002, PBT-07 infra).
/// Usa Testcontainers com PostgreSQL real.
/// Mapeia: TASK-14, RNF 5, DD-002, PBT-07.
/// </summary>
[Collection("PostgresCollection")]
public sealed class SnapshotTriggerTests(PostgresFixture fixture)
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<(Guid OpportunityId, Guid CommissionId)> SeedSnapshotAsync(Guid tenantId)
    {
        await using var ctx = await fixture.CreateContextAsync(tenantId);

        // Insere oportunidade
        var oppId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        await ctx.Database.ExecuteSqlRawAsync($@"
            INSERT INTO opportunities (
                id, tenant_id, bu_id, account_id,
                stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                probabilidade, stage_category, created_by, created_at, updated_at
            ) VALUES (
                '{oppId}', '{tenantId}', '{Guid.NewGuid()}', '{Guid.NewGuid()}',
                '{stageId}', 'Ganho', 'won', 100, 5,
                '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Parceiro', TRUE,
                'AZ-0099', 'Oportunidade Ganha', 100000, 0, 0,
                100, 'won', '{Guid.NewGuid()}', now(), now()
            )");

        // Insere snapshot imutável (is_snapshot = TRUE)
        var commId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        await ctx.Database.ExecuteSqlRawAsync($@"
            INSERT INTO opportunity_partner_commissions (
                id, tenant_id, opportunity_id, partner_id, role,
                pct_setup, pct_recorrente, valor_fixo, meses_comissionados,
                comissao_setup_cents, comissao_recorrente_cents, comissao_calculada,
                is_snapshot, snapshot_at, created_at
            ) VALUES (
                '{commId}', '{tenantId}', '{oppId}', '{partnerId}', 'Indicador',
                10.00, 5.00, 0, 12,
                10000, 60000, 70000,
                TRUE, now(), now()
            )");

        return (oppId, commId);
    }

    // =========================================================================
    // Teste 1: UPDATE em snapshot deve ser rejeitado pelo trigger (PBT-07 infra)
    // =========================================================================

    [Fact(DisplayName = "TRIGGER_01: UPDATE em is_snapshot=TRUE deve ser bloqueado pelo trigger trg_block_snapshot_mutation")]
    public async Task UpdateSnapshot_ShouldBeRejectedByTrigger()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (_, commId) = await SeedSnapshotAsync(tenantId);

        await using var ctx = await fixture.CreateContextAsync(tenantId);

        // Act & Assert: UPDATE deve lançar exceção do trigger
        var act = async () =>
            await ctx.Database.ExecuteSqlRawAsync($@"
                UPDATE opportunity_partner_commissions
                SET comissao_calculada = 99999
                WHERE id = '{commId}' AND is_snapshot = TRUE");

        await act.Should().ThrowAsync<PostgresException>(
            "o trigger trg_block_snapshot_mutation deve rejeitar UPDATE em snapshot (RNF 5, DD-002).")
            .WithMessage("*immutable*");
    }

    // =========================================================================
    // Teste 2: DELETE em snapshot deve ser rejeitado pelo trigger
    // =========================================================================

    [Fact(DisplayName = "TRIGGER_02: DELETE em is_snapshot=TRUE deve ser bloqueado pelo trigger")]
    public async Task DeleteSnapshot_ShouldBeRejectedByTrigger()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (_, commId) = await SeedSnapshotAsync(tenantId);

        await using var ctx = await fixture.CreateContextAsync(tenantId);

        // Act & Assert
        var act = async () =>
            await ctx.Database.ExecuteSqlRawAsync($@"
                DELETE FROM opportunity_partner_commissions
                WHERE id = '{commId}' AND is_snapshot = TRUE");

        await act.Should().ThrowAsync<PostgresException>(
            "o trigger deve rejeitar DELETE em snapshot (RNF 5).")
            .WithMessage("*immutable*");
    }

    // =========================================================================
    // Teste 3: UPDATE em registro não-snapshot deve ser permitido
    // =========================================================================

    [Fact(DisplayName = "TRIGGER_03: UPDATE em is_snapshot=FALSE (projetada) deve ser permitido")]
    public async Task UpdateProjected_ShouldBeAllowed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var ctx = await fixture.CreateContextAsync(tenantId);

        var oppId = Guid.NewGuid();
        await ctx.Database.ExecuteSqlRawAsync($@"
            INSERT INTO opportunities (
                id, tenant_id, bu_id, account_id,
                stage_id, stage_name, stage_category_ref, stage_default_probability, stage_order,
                owner_id, origin_channel_id, origin_channel_name, origin_is_partner_channel,
                opportunity_number, title, valor_setup, valor_mensal, duracao_meses,
                probabilidade, stage_category, created_by, created_at, updated_at
            ) VALUES (
                '{oppId}', '{tenantId}', '{Guid.NewGuid()}', '{Guid.NewGuid()}',
                '{Guid.NewGuid()}', 'Qualificação', 'open', 20, 1,
                '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Direto', FALSE,
                'AZ-0098', 'Opp Projetada', 50000, 0, 0,
                20, 'open', '{Guid.NewGuid()}', now(), now()
            )");

        var commId = Guid.NewGuid();
        await ctx.Database.ExecuteSqlRawAsync($@"
            INSERT INTO opportunity_partner_commissions (
                id, tenant_id, opportunity_id, partner_id, role,
                pct_setup, pct_recorrente, valor_fixo, meses_comissionados,
                comissao_setup_cents, comissao_recorrente_cents, comissao_calculada,
                is_snapshot, created_at
            ) VALUES (
                '{commId}', '{tenantId}', '{oppId}', '{Guid.NewGuid()}', 'Revendedor',
                10.00, 5.00, 0, 12,
                5000, 30000, 35000,
                FALSE, now()
            )");

        // Act: UPDATE em projetada (is_snapshot = FALSE) deve ser permitido
        var act = async () =>
            await ctx.Database.ExecuteSqlRawAsync($@"
                UPDATE opportunity_partner_commissions
                SET comissao_calculada = 40000
                WHERE id = '{commId}' AND is_snapshot = FALSE");

        await act.Should().NotThrowAsync(
            "o trigger deve permitir UPDATE em registros não-snapshot (projetada).");
    }

    // =========================================================================
    // Teste 4: constraint uq_partner_commission_active_snapshot impede 2 snapshots
    // =========================================================================

    [Fact(DisplayName = "TRIGGER_04: constraint uq_active_snapshot impede 2 snapshots para mesma oportunidade")]
    public async Task TwoSnapshots_ShouldViolateUniqueConstraint()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (oppId, _) = await SeedSnapshotAsync(tenantId);

        await using var ctx = await fixture.CreateContextAsync(tenantId);

        // Act: tenta inserir segundo snapshot para mesma oportunidade
        var act = async () =>
            await ctx.Database.ExecuteSqlRawAsync($@"
                INSERT INTO opportunity_partner_commissions (
                    id, tenant_id, opportunity_id, partner_id, role,
                    pct_setup, pct_recorrente, valor_fixo, meses_comissionados,
                    comissao_setup_cents, comissao_recorrente_cents, comissao_calculada,
                    is_snapshot, snapshot_at, created_at
                ) VALUES (
                    '{Guid.NewGuid()}', '{tenantId}', '{oppId}', '{Guid.NewGuid()}', 'Indicador',
                    10.00, 5.00, 0, 12,
                    10000, 60000, 70000,
                    TRUE, now(), now()
                )");

        // Assert: constraint uq_partner_commission_active_snapshot viola
        await act.Should().ThrowAsync<PostgresException>(
            "deve existir no máximo 1 snapshot por oportunidade (INV-12, RNF 5).")
            .WithMessage("*uq_partner_commission_active_snapshot*");
    }
}
