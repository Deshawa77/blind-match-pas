using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindMatchPAS.Data.Migrations
{
    /// <inheritdoc />
    public partial class StrengthenValidationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RoleType",
                table: "AspNetUsers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.DropIndex(
                name: "IX_MatchRecords_ProjectProposalId",
                table: "MatchRecords");

            migrationBuilder.CreateIndex(
                name: "IX_MatchRecords_ProjectProposalId",
                table: "MatchRecords",
                column: "ProjectProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResearchAreas_Name",
                table: "ResearchAreas",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorExpertise_SupervisorId_ResearchAreaId",
                table: "SupervisorExpertise",
                columns: new[] { "SupervisorId", "ResearchAreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorInterests_SupervisorId_ProjectProposalId",
                table: "SupervisorInterests",
                columns: new[] { "SupervisorId", "ProjectProposalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchRecords_ProjectProposalId",
                table: "MatchRecords");

            migrationBuilder.DropIndex(
                name: "IX_ResearchAreas_Name",
                table: "ResearchAreas");

            migrationBuilder.DropIndex(
                name: "IX_SupervisorExpertise_SupervisorId_ResearchAreaId",
                table: "SupervisorExpertise");

            migrationBuilder.DropIndex(
                name: "IX_SupervisorInterests_SupervisorId_ProjectProposalId",
                table: "SupervisorInterests");

            migrationBuilder.AlterColumn<string>(
                name: "RoleType",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_MatchRecords_ProjectProposalId",
                table: "MatchRecords",
                column: "ProjectProposalId");
        }
    }
}
