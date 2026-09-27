using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "schedules",
                columns: table => new
                {
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_as_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    cron_expression = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    overlap_policy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    input_json = table.Column<string>(type: "text", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_fire_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedules", x => x.schedule_id);
                    table.ForeignKey(
                        name: "FK_schedules_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schedule_runs",
                columns: table => new
                {
                    schedule_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_fire_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    manual = table.Column<bool>(type: "boolean", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_runs", x => x.schedule_run_id);
                    table.ForeignKey(
                        name: "FK_schedule_runs_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "schedules",
                        principalColumn: "schedule_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_schedule_runs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_runs_schedule_id_scheduled_fire_at",
                table: "schedule_runs",
                columns: new[] { "schedule_id", "scheduled_fire_at" },
                unique: true,
                filter: "\"scheduled_fire_at\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_runs_tenant_id",
                table: "schedule_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedules_enabled_deleted_at_next_fire_at",
                table: "schedules",
                columns: new[] { "enabled", "deleted_at", "next_fire_at" });

            migrationBuilder.CreateIndex(
                name: "IX_schedules_tenant_id",
                table: "schedules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedules_tenant_id_name",
                table: "schedules",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedule_runs");

            migrationBuilder.DropTable(
                name: "schedules");
        }
    }
}
