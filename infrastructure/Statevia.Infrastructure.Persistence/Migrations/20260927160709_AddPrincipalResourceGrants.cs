using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrincipalResourceGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "principal_resource_grants",
                columns: table => new
                {
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_principal_resource_grants", x => new { x.principal_id, x.resource_kind, x.resource_id });
                    table.CheckConstraint("ck_principal_resource_grants_resource_kind", "resource_kind IN ('project', 'definition')");
                    table.ForeignKey(
                        name: "FK_principal_resource_grants_principals_principal_id",
                        column: x => x.principal_id,
                        principalTable: "principals",
                        principalColumn: "principal_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_principal_resource_grants_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_principal_resource_grants_tenant_id",
                table: "principal_resource_grants",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "principal_resource_grants");
        }
    }
}
