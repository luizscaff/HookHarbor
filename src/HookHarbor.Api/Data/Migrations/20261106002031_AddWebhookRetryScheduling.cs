using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HookHarbor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookRetryScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "webhook_events",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "webhook_events");
        }
    }
}
