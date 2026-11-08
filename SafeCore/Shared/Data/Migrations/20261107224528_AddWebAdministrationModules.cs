using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeCore.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebAdministrationModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ComponentId",
                table: "SensorReadings",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ComponentId",
                table: "RiskSituations",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "RiskSituations",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ComponentId",
                table: "ResponseActions",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Devices",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "Devices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "Devices",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DeviceComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    InstalledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReplacedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceComponents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeviceConnectivityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ConnectivityStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceConnectivityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceConnectivityEvents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SafetyConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SingletonKey = table.Column<int>(type: "int", nullable: false),
                    SystemName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TimeZone = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CriticalAlertsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NotifyResponders = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ManualConfirmation = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TemperatureCelsius = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GasAdcRiseCounts = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AccelerationMagnitudeDeltaCounts = table.Column<double>(type: "double", nullable: false),
                    ImpactAlertMilliseconds = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyConfigurations", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeviceCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ComponentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CommandType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Result = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceCommands_DeviceComponents_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "DeviceComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceCommands_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PropertyId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zones_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PropertyId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ZoneId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAssignments_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAssignments_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_ComponentId",
                table: "SensorReadings",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSituations_ComponentId",
                table: "RiskSituations",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseActions_ComponentId",
                table: "ResponseActions",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CreatedByUserId",
                table: "Devices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ZoneId",
                table: "Devices",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_ComponentId",
                table: "DeviceCommands",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_DeviceId_Status_RequestedAt",
                table: "DeviceCommands",
                columns: new[] { "DeviceId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceComponents_DeviceId_Kind_Type_Channel",
                table: "DeviceComponents",
                columns: new[] { "DeviceId", "Kind", "Type", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceConnectivityEvents_DeviceId_OccurredAt",
                table: "DeviceConnectivityEvents",
                columns: new[] { "DeviceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Name",
                table: "Properties",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyConfigurations_SingletonKey",
                table: "SafetyConfigurations",
                column: "SingletonKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAssignments_PropertyId",
                table: "UserAssignments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAssignments_UserId_PropertyId_ZoneId",
                table: "UserAssignments",
                columns: new[] { "UserId", "PropertyId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAssignments_ZoneId",
                table: "UserAssignments",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_PropertyId_Name",
                table: "Zones",
                columns: new[] { "PropertyId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_AspNetUsers_CreatedByUserId",
                table: "Devices",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Zones_ZoneId",
                table: "Devices",
                column: "ZoneId",
                principalTable: "Zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResponseActions_DeviceComponents_ComponentId",
                table: "ResponseActions",
                column: "ComponentId",
                principalTable: "DeviceComponents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RiskSituations_DeviceComponents_ComponentId",
                table: "RiskSituations",
                column: "ComponentId",
                principalTable: "DeviceComponents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SensorReadings_DeviceComponents_ComponentId",
                table: "SensorReadings",
                column: "ComponentId",
                principalTable: "DeviceComponents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_AspNetUsers_CreatedByUserId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Zones_ZoneId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_ResponseActions_DeviceComponents_ComponentId",
                table: "ResponseActions");

            migrationBuilder.DropForeignKey(
                name: "FK_RiskSituations_DeviceComponents_ComponentId",
                table: "RiskSituations");

            migrationBuilder.DropForeignKey(
                name: "FK_SensorReadings_DeviceComponents_ComponentId",
                table: "SensorReadings");

            migrationBuilder.DropTable(
                name: "DeviceCommands");

            migrationBuilder.DropTable(
                name: "DeviceConnectivityEvents");

            migrationBuilder.DropTable(
                name: "SafetyConfigurations");

            migrationBuilder.DropTable(
                name: "UserAssignments");

            migrationBuilder.DropTable(
                name: "DeviceComponents");

            migrationBuilder.DropTable(
                name: "Zones");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_SensorReadings_ComponentId",
                table: "SensorReadings");

            migrationBuilder.DropIndex(
                name: "IX_RiskSituations_ComponentId",
                table: "RiskSituations");

            migrationBuilder.DropIndex(
                name: "IX_ResponseActions_ComponentId",
                table: "ResponseActions");

            migrationBuilder.DropIndex(
                name: "IX_Devices_CreatedByUserId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_ZoneId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ComponentId",
                table: "SensorReadings");

            migrationBuilder.DropColumn(
                name: "ComponentId",
                table: "RiskSituations");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "RiskSituations");

            migrationBuilder.DropColumn(
                name: "ComponentId",
                table: "ResponseActions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "AspNetUsers");
        }
    }
}
