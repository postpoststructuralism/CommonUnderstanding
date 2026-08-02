using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddBaselineContentGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsServiceAccount",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GenerationProvenanceJson",
                table: "SocialArguments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenerationSourceKey",
                table: "SocialArguments",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratorModel",
                table: "SocialArguments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratorPromptVersion",
                table: "SocialArguments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneratorProvider",
                table: "SocialArguments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAIGenerated",
                table: "SocialArguments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_generation_source",
                table: "SocialArguments",
                column: "GenerationSourceKey",
                unique: true,
                filter: "[GenerationSourceKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_socialarguments_generation_source",
                table: "SocialArguments");

            migrationBuilder.DropColumn(
                name: "IsServiceAccount",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "GenerationProvenanceJson",
                table: "SocialArguments");

            migrationBuilder.DropColumn(
                name: "GenerationSourceKey",
                table: "SocialArguments");

            migrationBuilder.DropColumn(
                name: "GeneratorModel",
                table: "SocialArguments");

            migrationBuilder.DropColumn(
                name: "GeneratorPromptVersion",
                table: "SocialArguments");

            migrationBuilder.DropColumn(
                name: "GeneratorProvider",
                table: "SocialArguments");

            migrationBuilder.DropColumn(
                name: "IsAIGenerated",
                table: "SocialArguments");
        }
    }
}
