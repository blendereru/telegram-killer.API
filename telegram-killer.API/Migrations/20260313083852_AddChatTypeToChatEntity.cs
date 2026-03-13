using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace telegram_killer.API.Migrations
{
    /// <inheritdoc />
    public partial class AddChatTypeToChatEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Chats",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Chats");
        }
    }
}
