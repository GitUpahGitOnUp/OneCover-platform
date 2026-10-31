using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCover.Claims.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutRequestedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PayoutRequestedAt",
                table: "BenefitClaims",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutRequestedAt",
                table: "BenefitClaims");
        }
    }
}
