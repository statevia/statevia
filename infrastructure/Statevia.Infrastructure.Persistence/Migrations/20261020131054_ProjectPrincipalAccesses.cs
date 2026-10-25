using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// <para><c>projects.owner_tenant_id</c> を <c>tenant_id</c> に改名し <c>visibility</c> を落とす。</para>
    /// <para><c>project_accesses</c> の付与先をテナントから Principal へ切り替える。旧テナント付与行は意味が変わるため削除する。</para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class ProjectPrincipalAccesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_project_accesses_tenants_tenant_id",
                table: "project_accesses");

            migrationBuilder.DropForeignKey(
                name: "FK_projects_tenants_owner_tenant_id",
                table: "projects");

            // 旧 tenant_id 付与は Principal 付与と意味が異なるため破棄する（default メンバーは別タスクでバックフィル）。
            migrationBuilder.Sql("DELETE FROM project_accesses;");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "projects");

            migrationBuilder.RenameColumn(
                name: "owner_tenant_id",
                table: "projects",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_projects_owner_tenant_id_slug",
                table: "projects",
                newName: "IX_projects_tenant_id_slug");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "project_accesses",
                newName: "principal_id");

            migrationBuilder.RenameIndex(
                name: "IX_project_accesses_tenant_id",
                table: "project_accesses",
                newName: "IX_project_accesses_principal_id");

            migrationBuilder.AddForeignKey(
                name: "FK_project_accesses_principals_principal_id",
                table: "project_accesses",
                column: "principal_id",
                principalTable: "principals",
                principalColumn: "principal_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_projects_tenants_tenant_id",
                table: "projects",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "tenant_id",
                onDelete: ReferentialAction.Restrict);

            // Project と Principal が同一テナントであること（FK だけでは足りない）。
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION enforce_project_access_same_tenant()
                RETURNS trigger AS $$
                BEGIN
                  IF NOT EXISTS (
                    SELECT 1
                    FROM projects p
                    INNER JOIN principals pr ON pr.principal_id = NEW.principal_id
                    WHERE p.project_id = NEW.project_id
                      AND p.tenant_id = pr.tenant_id
                  ) THEN
                    RAISE EXCEPTION 'project_accesses principal must belong to the project tenant';
                  END IF;
                  RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_project_accesses_same_tenant ON project_accesses;
                CREATE TRIGGER trg_project_accesses_same_tenant
                  BEFORE INSERT OR UPDATE ON project_accesses
                  FOR EACH ROW
                  EXECUTE FUNCTION enforce_project_access_same_tenant();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_project_accesses_same_tenant ON project_accesses;
                DROP FUNCTION IF EXISTS enforce_project_access_same_tenant();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_project_accesses_principals_principal_id",
                table: "project_accesses");

            migrationBuilder.DropForeignKey(
                name: "FK_projects_tenants_tenant_id",
                table: "projects");

            migrationBuilder.Sql("DELETE FROM project_accesses;");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "projects",
                newName: "owner_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_projects_tenant_id_slug",
                table: "projects",
                newName: "IX_projects_owner_tenant_id_slug");

            migrationBuilder.RenameColumn(
                name: "principal_id",
                table: "project_accesses",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_project_accesses_principal_id",
                table: "project_accesses",
                newName: "IX_project_accesses_tenant_id");

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "projects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.AddForeignKey(
                name: "FK_project_accesses_tenants_tenant_id",
                table: "project_accesses",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "tenant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_projects_tenants_owner_tenant_id",
                table: "projects",
                column: "owner_tenant_id",
                principalTable: "tenants",
                principalColumn: "tenant_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
