using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OneCover.Enrollment.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BenefitPercent = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    WeeklyMaximum = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EliminationDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MonthlyPremiumSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EnrolledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollments_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanRates",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MonthlyPremium = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanRates", x => new { x.PlanId, x.Tier });
                    table.ForeignKey(
                        name: "FK_PlanRates_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "BenefitPercent", "Description", "EliminationDays", "IsActive", "Name", "Type", "WeeklyMaximum" },
                values: new object[,]
                {
                    { 1, 0.60m, "Replaces part of your income during a short-term illness or injury.", 7, true, "Short-Term Disability", "STD", 1500m },
                    { 2, 0.60m, "Replaces part of your income during a long-term illness or injury.", 90, true, "Long-Term Disability", "LTD", 2300m },
                    { 3, 0m, "Sample Life coverage for eligible members.", 0, true, "Life", "Life", 0m },
                    { 4, 0m, "Sample Accident coverage for eligible members.", 0, true, "Accident", "Accident", 0m },
                    { 5, 0m, "Sample Dental coverage for eligible members.", 0, true, "Dental", "Dental", 0m },
                    { 6, 0m, "Sample Vision coverage for eligible members.", 0, true, "Vision", "Vision", 0m },
                    { 7, 0m, "Sample CriticalIllness coverage for eligible members.", 0, true, "Critical Illness", "CriticalIllness", 0m },
                    { 8, 0m, "Sample Pet coverage for eligible members.", 0, true, "Pet", "Pet", 0m }
                });

            migrationBuilder.InsertData(
                table: "PlanRates",
                columns: new[] { "PlanId", "Tier", "MonthlyPremium" },
                values: new object[,]
                {
                    { 1, "EmployeeOnly", 35m },
                    { 2, "EmployeeOnly", 25m },
                    { 3, "EmployeeChildren", 15.0m },
                    { 3, "EmployeeOnly", 10m },
                    { 3, "EmployeeSpouse", 17.0m },
                    { 3, "Family", 22.0m },
                    { 4, "EmployeeChildren", 22.5m },
                    { 4, "EmployeeOnly", 15m },
                    { 4, "EmployeeSpouse", 25.5m },
                    { 4, "Family", 33.0m },
                    { 5, "EmployeeChildren", 45.0m },
                    { 5, "EmployeeOnly", 30m },
                    { 5, "EmployeeSpouse", 51.0m },
                    { 5, "Family", 66.0m },
                    { 6, "EmployeeChildren", 18.0m },
                    { 6, "EmployeeOnly", 12m },
                    { 6, "EmployeeSpouse", 20.4m },
                    { 6, "Family", 26.4m },
                    { 7, "EmployeeChildren", 27.0m },
                    { 7, "EmployeeOnly", 18m },
                    { 7, "EmployeeSpouse", 30.6m },
                    { 7, "Family", 39.6m },
                    { 8, "EmployeeChildren", 12.0m },
                    { 8, "EmployeeOnly", 8m },
                    { 8, "EmployeeSpouse", 13.6m },
                    { 8, "Family", 17.6m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_MemberId_PlanId",
                table: "Enrollments",
                columns: new[] { "MemberId", "PlanId" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_PlanId",
                table: "Enrollments",
                column: "PlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "PlanRates");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
