using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindMatchPAS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectGroupSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectGroupId",
                table: "ProjectProposals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LeadStudentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectGroups_AspNetUsers_LeadStudentId",
                        column: x => x.LeadStudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectGroupId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsLead = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectGroupMembers_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectGroupMembers_ProjectGroups_ProjectGroupId",
                        column: x => x.ProjectGroupId,
                        principalTable: "ProjectGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGroupMembers_ProjectGroupId_StudentId",
                table: "ProjectGroupMembers",
                columns: new[] { "ProjectGroupId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGroupMembers_StudentId",
                table: "ProjectGroupMembers",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGroups_LeadStudentId",
                table: "ProjectGroups",
                column: "LeadStudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProposals_ProjectGroupId",
                table: "ProjectProposals",
                column: "ProjectGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectProposals_ProjectGroups_ProjectGroupId",
                table: "ProjectProposals",
                column: "ProjectGroupId",
                principalTable: "ProjectGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectProposals_ProjectGroups_ProjectGroupId",
                table: "ProjectProposals");

            migrationBuilder.DropTable(
                name: "ProjectGroupMembers");

            migrationBuilder.DropTable(
                name: "ProjectGroups");

            migrationBuilder.DropIndex(
                name: "IX_ProjectProposals_ProjectGroupId",
                table: "ProjectProposals");

            migrationBuilder.DropColumn(
                name: "ProjectGroupId",
                table: "ProjectProposals");
        }
    }
}
