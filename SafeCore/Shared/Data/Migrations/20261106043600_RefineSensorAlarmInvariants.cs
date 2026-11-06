using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeCore.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefineSensorAlarmInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SensorReading_ValueShape",
                table: "SensorReadings");

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "RiskSituations",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SensorReading_ValueShape",
                table: "SensorReadings",
                sql: "((SensorType IN ('Accelerometer', 'Gyroscope') AND MeasuredValue IS NULL AND AxisX BETWEEN -32768 AND 32767 AND AxisY BETWEEN -32768 AND 32767 AND AxisZ BETWEEN -32768 AND 32767 AND DigitalAlarm IS NULL) OR (SensorType = 'Gas' AND MeasuredValue BETWEEN 0 AND 4095 AND AxisX IS NULL AND AxisY IS NULL AND AxisZ IS NULL AND DigitalAlarm IS NOT NULL) OR (SensorType = 'Temperature' AND MeasuredValue BETWEEN -127 AND 125 AND AxisX IS NULL AND AxisY IS NULL AND AxisZ IS NULL AND DigitalAlarm IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SensorReading_ValueShape",
                table: "SensorReadings");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "RiskSituations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SensorReading_ValueShape",
                table: "SensorReadings",
                sql: "((SensorType IN ('Accelerometer', 'Gyroscope') AND MeasuredValue IS NULL AND AxisX IS NOT NULL AND AxisY IS NOT NULL AND AxisZ IS NOT NULL) OR (SensorType IN ('Gas', 'Temperature') AND MeasuredValue IS NOT NULL AND AxisX IS NULL AND AxisY IS NULL AND AxisZ IS NULL))");
        }
    }
}
