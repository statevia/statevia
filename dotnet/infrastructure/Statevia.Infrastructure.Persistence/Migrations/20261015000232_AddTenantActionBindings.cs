using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantActionBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_action_bindings",
                columns: table => new
                {
                    binding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    value_plaintext = table.Column<string>(type: "text", nullable: true),
                    value_protected = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_action_bindings", x => x.binding_id);
                    table.CheckConstraint("CK_tenant_action_bindings_kind_value", "(kind = 'variable' AND value_plaintext IS NOT NULL AND value_protected IS NULL) OR (kind = 'secret' AND value_plaintext IS NULL AND value_protected IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_tenant_action_bindings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_action_binding_actions",
                columns: table => new
                {
                    binding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_action_binding_actions", x => new { x.binding_id, x.action_id });
                    table.ForeignKey(
                        name: "FK_tenant_action_binding_actions_tenant_action_bindings_bindin~",
                        column: x => x.binding_id,
                        principalTable: "tenant_action_bindings",
                        principalColumn: "binding_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_action_bindings_tenant_id_name",
                table: "tenant_action_bindings",
                columns: new[] { "tenant_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_action_binding_actions");

            migrationBuilder.DropTable(
                name: "tenant_action_bindings");
        }
    }
}
