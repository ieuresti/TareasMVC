using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TareasMVC.Migrations
{
    /// <inheritdoc />
    public partial class AdminRol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            IF NOT EXISTS(SELECT Id FROM AspNetRoles WHERE Id = 'f1c673a0-b72c-4e2c-9151-7a5968956e0a')
            BEGIN
	            INSERT AspNetRoles (Id, [Name], [NormalizedName])
	            VALUES ('f1c673a0-b72c-4e2c-9151-7a5968956e0a', 'admin', 'ADMIN')
            END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE AspNetRoles WHERE Id = 'f1c673a0-b72c-4e2c-9151-7a5968956e0a'");
        }
    }
}
