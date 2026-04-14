using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindMatchPAS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupervisorInterests_SupervisorId",
                table: "SupervisorInterests");

            migrationBuilder.DropIndex(
                name: "IX_SupervisorExpertise_SupervisorId",
                table: "SupervisorExpertise");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SupervisorInterests_SupervisorId",
                table: "SupervisorInterests",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorExpertise_SupervisorId",
                table: "SupervisorExpertise",
                column: "SupervisorId");
        }
    }
}
