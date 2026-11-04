using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HookHarbor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateWebhookDeliveryAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_delivery_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    response_status_code = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<byte[]>(type: "bytea", nullable: true),
                    error_message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    destination_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_delivery_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_attempts_webhook_events_webhook_event_id",
                        column: x => x.webhook_event_id,
                        principalTable: "webhook_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_attempts_webhook_event_id_attempt_number",
                table: "webhook_delivery_attempts",
                columns: new[] { "webhook_event_id", "attempt_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_delivery_attempts");
        }
    }
}
