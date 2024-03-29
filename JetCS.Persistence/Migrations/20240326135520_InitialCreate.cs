using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetCS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Databases",
                columns: table => new
                {
                    DatabaseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Jet:Identity", "1, 1"),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    FilePath = table.Column<string>(type: "longchar", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Databases", x => x.DatabaseId);
                });

            migrationBuilder.CreateTable(
                name: "Logins",
                columns: table => new
                {
                    LoginId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Jet:Identity", "1, 1"),
                    LoginName = table.Column<string>(type: "varchar(255)", nullable: false),
                    Hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Salt = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "No")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PrimaryKey", x => x.LoginId);
                });

            migrationBuilder.CreateTable(
                name: "DatabaseLogins",
                columns: table => new
                {
                    DatabaseLoginId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Jet:Identity", "1, 1"),
                    DatabaseId = table.Column<int>(type: "integer", nullable: false),
                    LoginId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PrimaryKey", x => x.DatabaseLoginId);
                    table.ForeignKey(
                        name: "DatabasesDatabaseLogins",
                        column: x => x.DatabaseId,
                        principalTable: "Databases",
                        principalColumn: "DatabaseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "LoginsDatabaseLogins",
                        column: x => x.LoginId,
                        principalTable: "Logins",
                        principalColumn: "LoginId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "DatabaseLogin",
                table: "DatabaseLogins",
                columns: new[] { "DatabaseId", "LoginId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseLogins_LoginId",
                table: "DatabaseLogins",
                column: "LoginId");

            migrationBuilder.CreateIndex(
                name: "uniqlogin",
                table: "Logins",
                column: "LoginName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatabaseLogins");

            migrationBuilder.DropTable(
                name: "Databases");

            migrationBuilder.DropTable(
                name: "Logins");
        }
    }
}
