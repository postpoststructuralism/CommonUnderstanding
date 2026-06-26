using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpRelevanceAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FollowUpEffectivenessNotes",
                table: "SocialArguments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FollowUpRelevanceScore",
                table: "SocialArguments",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FollowUpEffectivenessNotes",
                table: "SocialArguments");

            migrationBuilder.DropColumn(
                name: "FollowUpRelevanceScore",
                table: "SocialArguments");
        }
    }
}
