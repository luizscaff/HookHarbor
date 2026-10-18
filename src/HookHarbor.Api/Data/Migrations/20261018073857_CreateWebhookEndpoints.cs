using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HookHarbor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateWebhookEndpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    inbound_key = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    destination_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_endpoints", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_inbound_key",
                table: "webhook_endpoints",
                column: "inbound_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_endpoints");
        }
    }
}
