using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── UnderstandingEdges: composite index for edge lookups ──
            // Covers: DetectEdgesAsync, DetectContradictionsAsync, DetectEdgesForArgumentAsync
            // Queries filter on (SourceNodeId, TargetNodeId) or single node lookups
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_understanding_edges_source_target
                    ON ""UnderstandingEdges"" (""SourceNodeId"", ""TargetNodeId"");
            ");

            // ── UnderstandingEdges: filtered index on relationship ──
            // Covers: DetectContradictionsAsync filtering on Relationship = 'contradicts'
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_understanding_edges_relationship
                    ON ""UnderstandingEdges"" (""Relationship"");
            ");

            // ── ArgumentVotes: composite index for vote lookups by argument ──
            // Covers: EpistemicScoringService.RecalculateAsync (grouping votes by ArgumentId)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_argument_votes_argument_id
                    ON ""ArgumentVotes"" (""ArgumentId"");
            ");

            // ── ArgumentVotes: composite index for user+time queries ──
            // Covers: EpistemicScoringWorker.CreateMissingProfilesAsync (recent votes by user)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_argument_votes_userid_createdat
                    ON ""ArgumentVotes"" (""UserId"", ""CreatedAt"" DESC);
            ");

            // ── UnderstandingNodes: index on NormalizedKey for key lookups ──
            // Covers: DetectContradictionsAsync, SyncAllAsync, DetectEdgesForArgumentAsync
            // All use NormalizeKey() for node matching
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_understanding_nodes_normalized_key
                    ON ""UnderstandingNodes"" (""NormalizedKey"");
            ");

            // ── SocialArguments: GIN index on Tags array ──
            // Covers: EpistemicScoringWorker.CreateMissingProfilesAsync (filtering by Tags[0])
            // GIN enables efficient array containment queries
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_social_arguments_tags_gin
                    ON ""SocialArguments"" USING GIN (""Tags"");
            ");

            // ── SchemaMemberships: composite index for schema lookups ──
            // Covers: LabelSchemasAsync (grouping memberships by SchemaId)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_schema_memberships_schema_id
                    ON ""SchemaMemberships"" (""SchemaId"", ""Weight"" DESC);
            ");

            // ── SchemaMemberships: index on NodeId for reverse lookups ──
            // Covers: queries finding which schemas a node belongs to
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_schema_memberships_node_id
                    ON ""SchemaMemberships"" (""NodeId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_understanding_edges_source_target;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_understanding_edges_relationship;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_argument_votes_argument_id;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_argument_votes_userid_createdat;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_understanding_nodes_normalized_key;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_social_arguments_tags_gin;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_schema_memberships_schema_id;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ix_schema_memberships_node_id;");
        }
    }
}
