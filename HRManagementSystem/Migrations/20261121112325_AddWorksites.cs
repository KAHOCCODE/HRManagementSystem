using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddWorksites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Worksites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Ward = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Region2026 = table.Column<int>(type: "int", nullable: true),
                    Region2027 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Worksites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorksiteAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    WorksiteId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorksiteAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorksiteAssignments_NhanVien_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "NhanVien",
                        principalColumn: "MaNV",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorksiteAssignments_Worksites_WorksiteId",
                        column: x => x.WorksiteId,
                        principalTable: "Worksites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorksiteAssignments_EmployeeId_EffectiveFrom",
                table: "WorksiteAssignments",
                columns: new[] { "EmployeeId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorksiteAssignments_WorksiteId",
                table: "WorksiteAssignments",
                column: "WorksiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Worksites_Code",
                table: "Worksites",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorksiteAssignments");

            migrationBuilder.DropTable(
                name: "Worksites");
        }
    }
}
