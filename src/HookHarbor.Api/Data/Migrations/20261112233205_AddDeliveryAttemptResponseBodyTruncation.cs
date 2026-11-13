using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HookHarbor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAttemptResponseBodyTruncation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "response_body_truncated",
                table: "webhook_delivery_attempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "response_body_truncated",
                table: "webhook_delivery_attempts");
        }
    }
}
