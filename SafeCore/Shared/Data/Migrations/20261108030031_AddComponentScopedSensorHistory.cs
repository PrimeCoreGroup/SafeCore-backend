using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeCore.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentScopedSensorHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_DeviceId_ComponentId_SensorType_CapturedAt",
                table: "SensorReadings",
                columns: new[] { "DeviceId", "ComponentId", "SensorType", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SensorReadings_DeviceId_ComponentId_SensorType_CapturedAt",
                table: "SensorReadings");
        }
    }
}
