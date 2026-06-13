using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Organization.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_units", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.message_id, x.tenant_id });
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    state = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    target_memberships = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_invitations", x => x.id);
                    table.CheckConstraint("chk_user_invitations_state", "state IN ('pending','accepted','revoked','expired')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    identity_uid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loss_reasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    bu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loss_reasons", x => x.id);
                    table.ForeignKey(
                        name: "FK_loss_reasons_business_units_bu_id",
                        column: x => x.bu_id,
                        principalTable: "business_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "origin_channels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    bu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_origin_channels", x => x.id);
                    table.ForeignKey(
                        name: "FK_origin_channels_business_units_bu_id",
                        column: x => x.bu_id,
                        principalTable: "business_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    probability = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    bu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stages", x => x.id);
                    table.CheckConstraint("chk_stages_category", "category IN ('open','won','lost')");
                    table.CheckConstraint("chk_stages_probability", "probability BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_stages_business_units_bu_id",
                        column: x => x.bu_id,
                        principalTable: "business_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_memberships", x => x.id);
                    table.CheckConstraint("chk_user_memberships_role", "role IN ('TAdmin','GestorBU','Vendedor','Viewer')");
                    table.ForeignKey(
                        name: "FK_user_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_business_units_tenant_id_name",
                table: "business_units",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loss_reasons_bu_id",
                table: "loss_reasons",
                column: "bu_id");

            migrationBuilder.CreateIndex(
                name: "uq_loss_reasons_tenant_bu_name",
                table: "loss_reasons",
                columns: new[] { "tenant_id", "bu_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_origin_channels_bu_id",
                table: "origin_channels",
                column: "bu_id");

            migrationBuilder.CreateIndex(
                name: "uq_origin_channels_tenant_bu_name",
                table: "origin_channels",
                columns: new[] { "tenant_id", "bu_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_outbox_events_unpublished",
                table: "outbox_events",
                columns: new[] { "tenant_id", "published_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_stages_bu_id",
                table: "stages",
                column: "bu_id");

            migrationBuilder.CreateIndex(
                name: "uq_stages_tenant_bu_name",
                table: "stages",
                columns: new[] { "tenant_id", "bu_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_stages_tenant_bu_position",
                table: "stages",
                columns: new[] { "tenant_id", "bu_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_user_invitations_tenant_token_hash",
                table: "user_invitations",
                columns: new[] { "tenant_id", "token_hash" });

            migrationBuilder.CreateIndex(
                name: "idx_user_memberships_tenant_user",
                table: "user_memberships",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_memberships_user_id",
                table: "user_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_memberships_tenant_user_bu",
                table: "user_memberships",
                columns: new[] { "tenant_id", "user_id", "bu_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_users_tenant_id_email",
                table: "users",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            // ── Row-Level Security (RLS) — ADR-0001, DEC-006 ──────────────────────────
            // Camada 2 do isolamento multi-tenant: RLS Postgres via SET app.current_tenant.
            // FORCE ROW LEVEL SECURITY garante que o owner da tabela (app user) também
            // seja submetido ao filtro RLS.
            // A policy usa current_setting com default NULL para retornar false quando o
            // parâmetro não está definido, impedindo acesso sem contexto de tenant.

            var tables = new[] { "business_units", "users", "user_invitations", "stages",
                "origin_channels", "loss_reasons", "user_memberships", "outbox_events",
                "inbox_messages" };

            foreach (var table in tables)
            {
                migrationBuilder.Sql($"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {table} FORCE ROW LEVEL SECURITY;");
            }

            // Tabelas com tenant_id direto: policy compara UUID
            var tenantTables = new[] { "business_units", "users", "user_invitations",
                "outbox_events" };

            foreach (var table in tenantTables)
            {
                migrationBuilder.Sql($@"
CREATE POLICY rls_{table}_tenant
    ON {table}
    USING (
        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
    );");
            }

            // Tabelas dependentes: usam a coluna tenant_id que é propagada via FK shadow property
            var dependentTables = new[] { "stages", "origin_channels", "loss_reasons",
                "user_memberships" };

            foreach (var table in dependentTables)
            {
                migrationBuilder.Sql($@"
CREATE POLICY rls_{table}_tenant
    ON {table}
    USING (
        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
    );");
            }

            // inbox_messages: chave composta com tenant_id
            migrationBuilder.Sql(@"
CREATE POLICY rls_inbox_messages_tenant
    ON inbox_messages
    USING (
        tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid
    );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "loss_reasons");

            migrationBuilder.DropTable(
                name: "origin_channels");

            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "stages");

            migrationBuilder.DropTable(
                name: "user_invitations");

            migrationBuilder.DropTable(
                name: "user_memberships");

            migrationBuilder.DropTable(
                name: "business_units");

            migrationBuilder.DropTable(
                name: "users");

            // ── Remover RLS ───────────────────────────────────────────────────────────
            var allTables = new[] { "business_units", "users", "user_invitations", "stages",
                "origin_channels", "loss_reasons", "user_memberships", "outbox_events",
                "inbox_messages" };

            foreach (var table in allTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS rls_{table}_tenant ON {table};");
                migrationBuilder.Sql($"ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;");
            }

            migrationBuilder.Sql("DROP POLICY IF EXISTS rls_inbox_messages_tenant ON inbox_messages;");
        }
    }
}
