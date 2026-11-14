using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionStartedAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_executions_tenant_id",
                table: "executions");

            migrationBuilder.CreateIndex(
                name: "IX_executions_definition_id_started_at",
                table: "executions",
                columns: new[] { "definition_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_executions_tenant_id_started_at",
                table: "executions",
                columns: new[] { "tenant_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_executions_definition_id_started_at",
                table: "executions");

            migrationBuilder.DropIndex(
                name: "IX_executions_tenant_id_started_at",
                table: "executions");

            migrationBuilder.CreateIndex(
                name: "IX_executions_tenant_id",
                table: "executions",
                column: "tenant_id");
        }
    }
}
