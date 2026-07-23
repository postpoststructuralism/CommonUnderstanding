using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260722000000_RemoveSyntheticGraphFillers")]
    public partial class RemoveSyntheticGraphFillers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    synthetic_user_id text;
                BEGIN
                    SELECT "Id" INTO synthetic_user_id
                    FROM "UserAccounts"
                    WHERE "Username" = 'understanding_graph';

                    IF synthetic_user_id IS NULL THEN
                        RETURN;
                    END IF;

                    DELETE FROM "SocialArgumentPropositions"
                    WHERE "ArgumentId" IN (
                        SELECT "Id"
                        FROM "SocialArguments"
                        WHERE "UserId" = synthetic_user_id
                          AND 'understanding-graph' = ANY("Tags")
                    );

                    DELETE FROM "SocialArguments"
                    WHERE "UserId" = synthetic_user_id
                      AND 'understanding-graph' = ANY("Tags");

                    DELETE FROM "SocialPropositions"
                    WHERE "UserId" = synthetic_user_id
                      AND "IsAIGenerated" = TRUE
                      AND NOT EXISTS (
                          SELECT 1
                          FROM "SocialArgumentPropositions" sap
                          WHERE sap."PropositionId" = "SocialPropositions"."Id"
                      );

                    DELETE FROM "UserAccounts"
                    WHERE "Id" = synthetic_user_id
                      AND "IsActive" = FALSE
                      AND NOT EXISTS (
                          SELECT 1 FROM "SocialArguments" sa WHERE sa."UserId" = synthetic_user_id
                      )
                      AND NOT EXISTS (
                          SELECT 1 FROM "SocialPropositions" sp WHERE sp."UserId" = synthetic_user_id
                      );
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}