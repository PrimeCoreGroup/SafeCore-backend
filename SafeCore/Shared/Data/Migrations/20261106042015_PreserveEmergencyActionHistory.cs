using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeCore.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreserveEmergencyActionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResponseActions_EmergencyResponses_EmergencyResponseId",
                table: "ResponseActions");

            migrationBuilder.AddForeignKey(
                name: "FK_ResponseActions_EmergencyResponses_EmergencyResponseId",
                table: "ResponseActions",
                column: "EmergencyResponseId",
                principalTable: "EmergencyResponses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResponseActions_EmergencyResponses_EmergencyResponseId",
                table: "ResponseActions");

            migrationBuilder.AddForeignKey(
                name: "FK_ResponseActions_EmergencyResponses_EmergencyResponseId",
                table: "ResponseActions",
                column: "EmergencyResponseId",
                principalTable: "EmergencyResponses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
