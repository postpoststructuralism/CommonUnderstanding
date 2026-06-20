using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommonUnderstanding.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2SocialEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EpistemicProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TopicDomain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EpistemicScore = table.Column<double>(type: "double precision", nullable: false),
                    VoteAccuracy = table.Column<double>(type: "double precision", nullable: false),
                    ContributionCount = table.Column<int>(type: "integer", nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpistemicProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModerationAppeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppellantUserId = table.Column<string>(type: "text", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationAppeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModerationFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlaggingUserId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Moderators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TopicDomain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GrantedByUserId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moderators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialPropositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IsAIGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    Embedding = table.Column<float[]>(type: "float4[]", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPropositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    XP = table.Column<long>(type: "bigint", nullable: false),
                    Rank = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Badges = table.Column<string[]>(type: "text[]", nullable: false),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    LongestStreak = table.Column<int>(type: "integer", nullable: false),
                    LastStreakDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StreakFreezes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Worldviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    SchwartzValues = table.Column<string[]>(type: "text[]", nullable: false),
                    SchwartzVector = table.Column<double[]>(type: "float8[]", nullable: false),
                    Embedding = table.Column<float[]>(type: "float4[]", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Worldviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XPTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReferenceEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XPTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DebateRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MotionText = table.Column<string>(type: "text", nullable: false),
                    MotionPropositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProponentUserId = table.Column<string>(type: "text", nullable: false),
                    OpponentUserId = table.Column<string>(type: "text", nullable: true),
                    JudgeUserIds = table.Column<string[]>(type: "text[]", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<string>(type: "text", nullable: false),
                    TimeLimitSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxContributionsPerSide = table.Column<int>(type: "integer", nullable: false),
                    ConcludedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProponentScore = table.Column<double>(type: "double precision", nullable: true),
                    OpponentScore = table.Column<double>(type: "double precision", nullable: true),
                    AIRefereeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebateRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DebateRooms_SocialPropositions_MotionPropositionId",
                        column: x => x.MotionPropositionId,
                        principalTable: "SocialPropositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SocialArguments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ClaimPropositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarrantText = table.Column<string>(type: "text", nullable: false),
                    ResolutionText = table.Column<string>(type: "text", nullable: true),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsShadowBanned = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpvoteCount = table.Column<int>(type: "integer", nullable: false),
                    DownvoteCount = table.Column<int>(type: "integer", nullable: false),
                    HotScore = table.Column<double>(type: "double precision", nullable: false),
                    WilsonScore = table.Column<double>(type: "double precision", nullable: false),
                    ControversyScore = table.Column<double>(type: "double precision", nullable: false),
                    IsAIValidated = table.Column<bool>(type: "boolean", nullable: false),
                    AIValidityScore = table.Column<double>(type: "double precision", nullable: true),
                    AIFallacyFlags = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    SchwartzValues = table.Column<string[]>(type: "text[]", nullable: false),
                    Embedding = table.Column<float[]>(type: "float4[]", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialArguments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialArguments_SocialPropositions_ClaimPropositionId",
                        column: x => x.ClaimPropositionId,
                        principalTable: "SocialPropositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorldviewVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Vote = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldviewVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldviewVotes_Worldviews_WorldviewId",
                        column: x => x.WorldviewId,
                        principalTable: "Worldviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArgumentChains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RootArgumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    ArgumentIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Embedding = table.Column<float[]>(type: "float4[]", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentChains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArgumentChains_SocialArguments_RootArgumentId",
                        column: x => x.RootArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArgumentLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceArgumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetArgumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkType = table.Column<string>(type: "text", nullable: false),
                    Annotation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentLinks", x => x.Id);
                    table.CheckConstraint("CK_ArgumentLinks_NoSelfLoop", "\"SourceArgumentId\" <> \"TargetArgumentId\"");
                    table.ForeignKey(
                        name: "FK_ArgumentLinks_SocialArguments_SourceArgumentId",
                        column: x => x.SourceArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArgumentLinks_SocialArguments_TargetArgumentId",
                        column: x => x.TargetArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArgumentVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Vote = table.Column<string>(type: "text", nullable: false),
                    Rationale = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EpistemicWeight = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArgumentVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArgumentVotes_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DebateContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DebateRoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ArgumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    FallacyFlags = table.Column<string>(type: "text", nullable: true),
                    ValidityScore = table.Column<double>(type: "double precision", nullable: true),
                    AIRefereeComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebateContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DebateContributions_DebateRooms_DebateRoomId",
                        column: x => x.DebateRoomId,
                        principalTable: "DebateRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DebateContributions_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SocialArgumentPropositions",
                columns: table => new
                {
                    ArgumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialArgumentPropositions", x => new { x.ArgumentId, x.PropositionId });
                    table.ForeignKey(
                        name: "FK_SocialArgumentPropositions_SocialArguments_ArgumentId",
                        column: x => x.ArgumentId,
                        principalTable: "SocialArguments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialArgumentPropositions_SocialPropositions_PropositionId",
                        column: x => x.PropositionId,
                        principalTable: "SocialPropositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorldviewChains",
                columns: table => new
                {
                    WorldviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArgumentChainId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldviewChains", x => new { x.WorldviewId, x.ArgumentChainId });
                    table.ForeignKey(
                        name: "FK_WorldviewChains_ArgumentChains_ArgumentChainId",
                        column: x => x.ArgumentChainId,
                        principalTable: "ArgumentChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorldviewChains_Worldviews_WorldviewId",
                        column: x => x.WorldviewId,
                        principalTable: "Worldviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentChains_RootArgumentId",
                table: "ArgumentChains",
                column: "RootArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentLinks_SourceArgumentId",
                table: "ArgumentLinks",
                column: "SourceArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentLinks_TargetArgumentId",
                table: "ArgumentLinks",
                column: "TargetArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArgumentVotes_ArgumentId_UserId",
                table: "ArgumentVotes",
                columns: new[] { "ArgumentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebateContributions_ArgumentId",
                table: "DebateContributions",
                column: "ArgumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DebateContributions_DebateRoomId",
                table: "DebateContributions",
                column: "DebateRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_DebateRooms_MotionPropositionId",
                table: "DebateRooms",
                column: "MotionPropositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EpistemicProfiles_UserId_TopicDomain",
                table: "EpistemicProfiles",
                columns: new[] { "UserId", "TopicDomain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationAppeals_AppellantUserId",
                table: "ModerationAppeals",
                column: "AppellantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationFlags_EntityType_EntityId",
                table: "ModerationFlags",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationFlags_Status",
                table: "ModerationFlags",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Moderators_UserId_TopicDomain",
                table: "Moderators",
                columns: new[] { "UserId", "TopicDomain" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialArgumentPropositions_PropositionId",
                table: "SocialArgumentPropositions",
                column: "PropositionId");

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_createdat",
                table: "SocialArguments",
                column: "CreatedAt",
                filter: "\"IsPublic\" = true AND \"IsShadowBanned\" = false");

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_hotscore",
                table: "SocialArguments",
                column: "HotScore",
                filter: "\"IsPublic\" = true AND \"IsShadowBanned\" = false");

            migrationBuilder.CreateIndex(
                name: "idx_socialarguments_wilsonscore",
                table: "SocialArguments",
                column: "WilsonScore",
                filter: "\"IsPublic\" = true AND \"IsShadowBanned\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SocialArguments_ClaimPropositionId",
                table: "SocialArguments",
                column: "ClaimPropositionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_UserId",
                table: "UserReputations",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorldviewChains_ArgumentChainId",
                table: "WorldviewChains",
                column: "ArgumentChainId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldviewVotes_WorldviewId_UserId",
                table: "WorldviewVotes",
                columns: new[] { "WorldviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XPTransactions_UserId",
                table: "XPTransactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArgumentLinks");

            migrationBuilder.DropTable(
                name: "ArgumentVotes");

            migrationBuilder.DropTable(
                name: "DebateContributions");

            migrationBuilder.DropTable(
                name: "EpistemicProfiles");

            migrationBuilder.DropTable(
                name: "ModerationAppeals");

            migrationBuilder.DropTable(
                name: "ModerationFlags");

            migrationBuilder.DropTable(
                name: "Moderators");

            migrationBuilder.DropTable(
                name: "SocialArgumentPropositions");

            migrationBuilder.DropTable(
                name: "UserReputations");

            migrationBuilder.DropTable(
                name: "WorldviewChains");

            migrationBuilder.DropTable(
                name: "WorldviewVotes");

            migrationBuilder.DropTable(
                name: "XPTransactions");

            migrationBuilder.DropTable(
                name: "DebateRooms");

            migrationBuilder.DropTable(
                name: "ArgumentChains");

            migrationBuilder.DropTable(
                name: "Worldviews");

            migrationBuilder.DropTable(
                name: "SocialArguments");

            migrationBuilder.DropTable(
                name: "SocialPropositions");
        }
    }
}
