using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceArgumentIdToSocialArgument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceArgumentId",
                table: "SocialArguments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_sourceargumentid",
                table: "SocialArguments",
                column: "SourceArgumentId",
                unique: true,
                filter: "\"SourceArgumentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_socialarguments_sourceargumentid",
                table: "SocialArguments");

            migrationBuilder.DropColumn(
                name: "SourceArgumentId",
                table: "SocialArguments");
        }
    }
}
