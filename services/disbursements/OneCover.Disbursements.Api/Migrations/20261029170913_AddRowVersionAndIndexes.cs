using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneCover.Disbursements.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Disbursements_MemberId",
                table: "Disbursements");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Disbursements",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_MemberId_DueDate",
                table: "Disbursements",
                columns: new[] { "MemberId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_Status",
                table: "Disbursements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Disbursements_MemberId_DueDate",
                table: "Disbursements");

            migrationBuilder.DropIndex(
                name: "IX_Disbursements_Status",
                table: "Disbursements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Disbursements");

            migrationBuilder.CreateIndex(
                name: "IX_Disbursements_MemberId",
                table: "Disbursements",
                column: "MemberId");
        }
    }
}
