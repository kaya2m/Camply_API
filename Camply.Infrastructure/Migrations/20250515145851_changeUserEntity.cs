using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camply.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class changeUserEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PasswordResetTokenExpiry",
                table: "Users",
                newName: "PasswordResetCodeExpiry");

            migrationBuilder.RenameColumn(
                name: "PasswordResetToken",
                table: "Users",
                newName: "PasswordResetCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PasswordResetCodeExpiry",
                table: "Users",
                newName: "PasswordResetTokenExpiry");

            migrationBuilder.RenameColumn(
                name: "PasswordResetCode",
                table: "Users",
                newName: "PasswordResetToken");
        }
    }
}
