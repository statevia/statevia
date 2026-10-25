using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// 既存 <c>default</c> のユーザーと API キー Principal を <c>admin</c> で一度入れる。
    /// 既存メンバーは上書きしない。Down では外さない（後から付けた admin と区別できない）。
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class BackfillDefaultProjectAdmins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(
                """
                INSERT INTO project_accesses (project_id, principal_id, role, created_at)
                SELECT p.project_id, pr.principal_id, 'Admin', NOW()
                FROM projects p
                INNER JOIN principals pr ON pr.tenant_id = p.tenant_id
                WHERE p.slug = 'default'
                  AND pr.deleted_at IS NULL
                  AND (
                    pr.principal_type = 'User'
                    OR EXISTS (
                      SELECT 1 FROM api_keys k WHERE k.principal_id = pr.principal_id
                    )
                  )
                ON CONFLICT (project_id, principal_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
        }
    }
}
