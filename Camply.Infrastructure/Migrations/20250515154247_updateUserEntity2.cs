using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Camply.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updateUserEntity2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CodeVerifiedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPasswordResetCodeVerified",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeVerifiedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsPasswordResetCodeVerified",
                table: "Users");
        }
    }
}
