using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityPipeline.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration: cria o trigger trg_block_snapshot_mutation e o índice uq_active_snapshot.
/// Garante imutabilidade do snapshot de comissão no banco (RNF 5, DD-002, PBT-07).
/// Esta migration é manual (o scaffolding do EF não gera triggers).
/// Mapeia: design §7.3, design §6.1, TASK-14.
/// </summary>
public partial class SnapshotTrigger : Migration
{
    /// <inheritdoc/>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Função de trigger que rejeita UPDATE/DELETE em snapshots (RNF 5, DD-002)
        migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION block_snapshot_mutation()
RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'DELETE' AND OLD.is_snapshot THEN
        RAISE EXCEPTION 'commission snapshot is immutable (RN-007/RN-022) — DELETE negado';
    END IF;
    IF TG_OP = 'UPDATE' AND OLD.is_snapshot THEN
        RAISE EXCEPTION 'commission snapshot is immutable (RN-007/RN-022) — UPDATE negado';
    END IF;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;
");

        // Trigger chamado BEFORE UPDATE OR DELETE em cada linha
        migrationBuilder.Sql(@"
CREATE TRIGGER trg_block_snapshot_mutation
    BEFORE UPDATE OR DELETE ON opportunity_partner_commissions
    FOR EACH ROW
    EXECUTE FUNCTION block_snapshot_mutation();
");

        // REVOKE DELETE no role app para transições (RNF 7, RNF 8.2)
        // O índice uq_partner_commission_active_snapshot já foi criado em InitialSchema
        // A constraint uq_active_snapshot é reforço adicional via trigger.
    }

    /// <inheritdoc/>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS trg_block_snapshot_mutation ON opportunity_partner_commissions;");
        migrationBuilder.Sql(
            "DROP FUNCTION IF EXISTS block_snapshot_mutation;");
    }
}
