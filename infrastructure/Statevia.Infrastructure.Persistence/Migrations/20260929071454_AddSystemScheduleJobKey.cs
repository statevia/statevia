using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemScheduleJobKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_schedules_tenant_id_name",
                table: "schedules");

            migrationBuilder.AlterColumn<Guid>(
                name: "run_as_principal_id",
                table: "schedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "definition_id",
                table: "schedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "job_key",
                table: "schedules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary_json",
                table: "schedule_runs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedules_tenant_id_job_key",
                table: "schedules",
                columns: new[] { "tenant_id", "job_key" },
                unique: true,
                filter: "\"job_key\" IS NOT NULL AND \"deleted_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_schedules_tenant_id_name",
                table: "schedules",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "\"job_key\" IS NULL AND \"deleted_at\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_schedules_job_key",
                table: "schedules",
                sql: "job_key IS NULL OR job_key = 'stuck-execution-report'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_schedules_job_shape",
                table: "schedules",
                sql: "(job_key IS NULL AND definition_id IS NOT NULL AND run_as_principal_id IS NOT NULL)\r\nOR (job_key IS NOT NULL AND definition_id IS NULL AND run_as_principal_id IS NULL AND input_json IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_schedules_tenant_id_job_key",
                table: "schedules");

            migrationBuilder.DropIndex(
                name: "IX_schedules_tenant_id_name",
                table: "schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_schedules_job_key",
                table: "schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_schedules_job_shape",
                table: "schedules");

            migrationBuilder.DropColumn(
                name: "job_key",
                table: "schedules");

            migrationBuilder.DropColumn(
                name: "summary_json",
                table: "schedule_runs");

            migrationBuilder.AlterColumn<Guid>(
                name: "run_as_principal_id",
                table: "schedules",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "definition_id",
                table: "schedules",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedules_tenant_id_name",
                table: "schedules",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");
        }
    }
}
