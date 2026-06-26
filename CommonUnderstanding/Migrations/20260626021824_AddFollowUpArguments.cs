using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpArguments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReplyCount",
                table: "SocialArguments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplyCount",
                table: "SocialArguments");
        }
    }
}
