using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeCore.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticSensorRiskAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SensorReadings_Devices_DeviceId",
                table: "SensorReadings");

            migrationBuilder.DropIndex(
                name: "IX_SensorReadings_DeviceId_CapturedAt",
                table: "SensorReadings");

            migrationBuilder.AddColumn<bool>(
                name: "DigitalAlarm",
                table: "SensorReadings",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AlertUntil",
                table: "RiskSituations",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "RiskSituations",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_DeviceId_SensorType_CapturedAt",
                table: "SensorReadings",
                columns: new[] { "DeviceId", "SensorType", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskSituations_DeviceId_RiskType_Status_DetectedAt",
                table: "RiskSituations",
                columns: new[] { "DeviceId", "RiskType", "Status", "DetectedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_SensorReadings_Devices_DeviceId",
                table: "SensorReadings",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RiskSituations_Devices_DeviceId",
                table: "RiskSituations",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SensorReadings_Devices_DeviceId",
                table: "SensorReadings");

            migrationBuilder.DropForeignKey(
                name: "FK_RiskSituations_Devices_DeviceId",
                table: "RiskSituations");

            migrationBuilder.DropIndex(
                name: "IX_SensorReadings_DeviceId_SensorType_CapturedAt",
                table: "SensorReadings");

            migrationBuilder.DropIndex(
                name: "IX_RiskSituations_DeviceId_RiskType_Status_DetectedAt",
                table: "RiskSituations");

            migrationBuilder.DropColumn(
                name: "DigitalAlarm",
                table: "SensorReadings");

            migrationBuilder.DropColumn(
                name: "AlertUntil",
                table: "RiskSituations");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "RiskSituations");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_DeviceId_CapturedAt",
                table: "SensorReadings",
                columns: new[] { "DeviceId", "CapturedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_SensorReadings_Devices_DeviceId",
                table: "SensorReadings",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
