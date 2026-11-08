using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeCore.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdjustConnectionHistoryRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceConnectivityEvents_Devices_DeviceId",
                table: "DeviceConnectivityEvents");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceConnectivityEvents_Devices_DeviceId",
                table: "DeviceConnectivityEvents",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceConnectivityEvents_Devices_DeviceId",
                table: "DeviceConnectivityEvents");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceConnectivityEvents_Devices_DeviceId",
                table: "DeviceConnectivityEvents",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
