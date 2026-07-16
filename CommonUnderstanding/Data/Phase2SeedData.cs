using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Data;

/// <summary>
/// Seeds the database with sample Phase 2 data for testing.
/// Phase 1 (4.6): Opt-in only — run with `dotnet run --seed` or set SEED_SAMPLE_DATA=true.
/// No test data ships to production.
/// </summary>
public static class Phase2SeedData
{
    public static async Task SeedAllAsync(ApplicationDbContext db, ILogger logger)
    {
        logger.LogInformation("Starting Phase 2 seed data...");

        // Only seed if no data exists
        if (await db.SocialPropositions.AnyAsync())
        {
            logger.LogInformation("Phase 2 data already exists, skipping seed.");
            return;
        }

        // Ensure we have at least one user
        var user = await db.UserAccounts.FirstOrDefaultAsync();
        if (user == null)
        {
            var hasher = new PasswordHasher<UserAccount>();
            user = new UserAccount
            {
                Id = Guid.NewGuid().ToString(),
                Username = "demo_user",
                DisplayName = "Demo User",
                PasswordHash = hasher.HashPassword(null!, "demo123"),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            db.UserAccounts.Add(user);
            await db.SaveChangesAsync();
            logger.LogInformation("Created demo user: {Username}", user.Username);
        }

        var userId = user.Id;

        // ── Create Propositions ──────────────────────────────────────────
        var props = new[]
        {
            new SocialProposition { Text = "Universal Basic Income reduces poverty.", Type = SocialPropositionType.Claim, UserId = userId, IsConfirmed = true },
            new SocialProposition { Text = "The Stockton SEED trial showed median household income rose 28%.", Type = SocialPropositionType.Evidence, UserId = userId, IsConfirmed = true, SourceUrl = "https://www.stocktondemonstration.org" },
            new SocialProposition { Text = "If income supplementation reduces poverty in controlled trials, then UBI reduces poverty.", Type = SocialPropositionType.Warrant, UserId = userId, IsConfirmed = true },
            new SocialProposition { Text = "The Stockton trial had selection bias; participants were not randomly sampled.", Type = SocialPropositionType.Rebuttal, UserId = userId, IsConfirmed = true },
            new SocialProposition { Text = "Climate change is primarily driven by human activity.", Type = SocialPropositionType.Claim, UserId = userId, IsConfirmed = true },
            new SocialProposition { Text = "IPCC AR6 report states human influence has warmed the atmosphere, ocean, and land.", Type = SocialPropositionType.Evidence, UserId = userId, IsConfirmed = true, SourceUrl = "https://www.ipcc.ch/report/ar6/wg1/" },
            new SocialProposition { Text = "Free speech is essential for democratic societies.", Type = SocialPropositionType.Claim, UserId = userId, IsConfirmed = true },
            new SocialProposition { Text = "Countries with strong free speech protections score higher on the Democracy Index.", Type = SocialPropositionType.Evidence, UserId = userId, IsConfirmed = true, SourceUrl = "https://www.eiu.com/n/campaigns/democracy-index-2023/" },
            new SocialProposition { Text = "Artificial Intelligence will create more jobs than it destroys.", Type = SocialPropositionType.Claim, UserId = userId, IsConfirmed = true },
            new SocialProposition { Text = "The World Economic Forum predicts AI will displace 85M jobs but create 97M new ones by 2025.", Type = SocialPropositionType.Evidence, UserId = userId, IsConfirmed = true },
        };
        db.SocialPropositions.AddRange(props);
        await db.SaveChangesAsync();
        logger.LogInformation("Created {Count} propositions.", props.Length);

        // ── Create SocialArguments ───────────────────────────────────────
        var args = new[]
        {
            new SocialArgument
            {
                Title = "UBI Reduces Poverty: Evidence from Stockton",
                ClaimPropositionId = props[0].Id,
                WarrantText = "Controlled trials provide the strongest empirical evidence for policy effectiveness.",
                ResolutionText = "UBI should be implemented as a poverty reduction strategy.",
                IsPublic = true,
                UserId = userId,
                Tags = new[] { "economics", "poverty", "ubi" },
                SchwartzValues = new[] { "Universalism", "Security" },
                UpvoteCount = 15,
                DownvoteCount = 3,
                HotScore = 12.5,
                WilsonScore = 0.78,
                IsAIValidated = true,
                AIValidityScore = 0.85,
            },
            new SocialArgument
            {
                Title = "Human-Caused Climate Change: The Scientific Consensus",
                ClaimPropositionId = props[4].Id,
                WarrantText = "The IPCC represents the consensus of thousands of climate scientists worldwide.",
                ResolutionText = "Immediate action on emissions is necessary.",
                IsPublic = true,
                UserId = userId,
                Tags = new[] { "climate", "science", "environment" },
                SchwartzValues = new[] { "Universalism", "Security" },
                UpvoteCount = 22,
                DownvoteCount = 5,
                HotScore = 18.2,
                WilsonScore = 0.82,
                IsAIValidated = true,
                AIValidityScore = 0.91,
            },
            new SocialArgument
            {
                Title = "Free Speech as a Democratic Foundation",
                ClaimPropositionId = props[6].Id,
                WarrantText = "Democratic governance requires open exchange of ideas and criticism of power.",
                ResolutionText = "Free speech protections should be strengthened globally.",
                IsPublic = true,
                UserId = userId,
                Tags = new[] { "politics", "democracy", "rights" },
                SchwartzValues = new[] { "SelfDirection", "Universalism" },
                UpvoteCount = 18,
                DownvoteCount = 8,
                HotScore = 14.1,
                WilsonScore = 0.71,
                IsAIValidated = true,
                AIValidityScore = 0.79,
            },
            new SocialArgument
            {
                Title = "AI Job Creation vs. Displacement",
                ClaimPropositionId = props[8].Id,
                WarrantText = "Historical technological revolutions have consistently created more jobs than they destroyed.",
                ResolutionText = "Policy should focus on retraining, not restriction.",
                IsPublic = true,
                UserId = userId,
                Tags = new[] { "technology", "economics", "ai" },
                SchwartzValues = new[] { "Achievement", "Stimulation" },
                UpvoteCount = 10,
                DownvoteCount = 12,
                HotScore = 5.3,
                WilsonScore = 0.48,
                ControversyScore = 0.85,
                IsAIValidated = true,
                AIValidityScore = 0.62,
            },
        };
        db.SocialArguments.AddRange(args);
        await db.SaveChangesAsync();
        logger.LogInformation("Created {Count} social arguments.", args.Length);

        // ── Link arguments to propositions ──────────────────────────────
        var argProps = new[]
        {
            new SocialArgumentProposition { ArgumentId = args[0].Id, PropositionId = props[0].Id, Role = SocialPropositionType.Claim, OrderIndex = 0 },
            new SocialArgumentProposition { ArgumentId = args[0].Id, PropositionId = props[1].Id, Role = SocialPropositionType.Evidence, OrderIndex = 1 },
            new SocialArgumentProposition { ArgumentId = args[0].Id, PropositionId = props[2].Id, Role = SocialPropositionType.Warrant, OrderIndex = 2 },
            new SocialArgumentProposition { ArgumentId = args[0].Id, PropositionId = props[3].Id, Role = SocialPropositionType.Rebuttal, OrderIndex = 3 },
            new SocialArgumentProposition { ArgumentId = args[1].Id, PropositionId = props[4].Id, Role = SocialPropositionType.Claim, OrderIndex = 0 },
            new SocialArgumentProposition { ArgumentId = args[1].Id, PropositionId = props[5].Id, Role = SocialPropositionType.Evidence, OrderIndex = 1 },
            new SocialArgumentProposition { ArgumentId = args[2].Id, PropositionId = props[6].Id, Role = SocialPropositionType.Claim, OrderIndex = 0 },
            new SocialArgumentProposition { ArgumentId = args[2].Id, PropositionId = props[7].Id, Role = SocialPropositionType.Evidence, OrderIndex = 1 },
            new SocialArgumentProposition { ArgumentId = args[3].Id, PropositionId = props[8].Id, Role = SocialPropositionType.Claim, OrderIndex = 0 },
            new SocialArgumentProposition { ArgumentId = args[3].Id, PropositionId = props[9].Id, Role = SocialPropositionType.Evidence, OrderIndex = 1 },
        };
        db.SocialArgumentPropositions.AddRange(argProps);
        await db.SaveChangesAsync();
        logger.LogInformation("Created {Count} argument-proposition links.", argProps.Length);

        // ── Create ArgumentLinks ─────────────────────────────────────────
        var links = new[]
        {
            new ArgumentLink { SourceArgumentId = args[1].Id, TargetArgumentId = args[0].Id, LinkType = LinkType.Supports, UserId = userId, Annotation = "Climate and poverty are interconnected crises." },
            new ArgumentLink { SourceArgumentId = args[2].Id, TargetArgumentId = args[0].Id, LinkType = LinkType.Extends, UserId = userId, Annotation = "Free speech enables better policy debate on UBI." },
            new ArgumentLink { SourceArgumentId = args[3].Id, TargetArgumentId = args[1].Id, LinkType = LinkType.Refines, UserId = userId, Annotation = "AI could help model climate solutions." },
        };
        db.ArgumentLinks.AddRange(links);
        await db.SaveChangesAsync();
        logger.LogInformation("Created {Count} argument links.", links.Length);

        // ── Create ArgumentChain ─────────────────────────────────────────
        var chain = new ArgumentChain
        {
            Title = "Evidence-Based Policy for the 21st Century",
            Description = "A chain connecting UBI, climate, and free speech arguments into a coherent policy framework.",
            RootArgumentId = args[0].Id,
            IsPublic = true,
            UserId = userId,
            Tags = new[] { "policy", "evidence-based" },
            ArgumentIds = new[] { args[0].Id, args[1].Id, args[2].Id },
        };
        db.ArgumentChains.Add(chain);
        await db.SaveChangesAsync();
        logger.LogInformation("Created argument chain: {Title}", chain.Title);

        // ── Create Worldview ─────────────────────────────────────────────
        var worldview = new Worldview
        {
            Title = "Progressive Empiricism",
            Description = "A worldview grounded in evidence-based policy, scientific consensus, and democratic values.",
            UserId = userId,
            IsPublic = true,
            Tags = new[] { "progressivism", "empiricism", "democracy" },
            SchwartzValues = new[] { "Universalism", "SelfDirection", "Benevolence", "Security" },
            SchwartzVector = new[] { 0.9, 0.7, 0.6, 0.3, 0.5, 0.1, 0.2, 0.1, 0.4, 0.8 },
        };
        db.Worldviews.Add(worldview);
        await db.SaveChangesAsync();
        logger.LogInformation("Created worldview: {Title}", worldview.Title);

        // ── Link chain to worldview ─────────────────────────────────────
        db.WorldviewChains.Add(new WorldviewChain { WorldviewId = worldview.Id, ArgumentChainId = chain.Id, OrderIndex = 0 });
        await db.SaveChangesAsync();

        // ── Create DebateRoom ────────────────────────────────────────────
        var debate = new DebateRoom
        {
            Title = "Is UBI the Best Anti-Poverty Tool?",
            Topic = "Economics",
            MotionText = "Universal Basic Income is the most effective anti-poverty intervention available to modern governments.",
            MotionPropositionId = props[0].Id,
            ProponentUserId = userId,
            Format = DebateFormat.Oxford,
            Status = DebateStatus.Open,
            JudgeUserIds = Array.Empty<string>(),
        };
        db.DebateRooms.Add(debate);
        await db.SaveChangesAsync();
        logger.LogInformation("Created debate room: {Title}", debate.Title);

        // ── Create UserReputation ────────────────────────────────────────
        var rep = new UserReputation
        {
            UserId = userId,
            XP = 250,
            CurrentStreak = 5,
            LongestStreak = 12,
            Badges = new[] { "FirstArgument", "ChainBuilder", "EarlyAdopter" },
            Rank = "Reasoner",
        };
        db.UserReputations.Add(rep);
        await db.SaveChangesAsync();
        logger.LogInformation("Created user reputation for {UserId}", userId);

        // ── Create EpistemicProfile ──────────────────────────────────────
        var epProfile = new EpistemicProfile
        {
            UserId = userId,
            TopicDomain = "economics",
            EpistemicScore = 3.5,
            VoteAccuracy = 0.82,
            ContributionCount = 4,
            VoteCount = 25,
        };
        db.EpistemicProfiles.Add(epProfile);
        await db.SaveChangesAsync();
        logger.LogInformation("Created epistemic profile for {UserId} in {Domain}", userId, "economics");

        logger.LogInformation("Phase 2 seed data complete! Created:");
        logger.LogInformation("  - {A} propositions", props.Length);
        logger.LogInformation("  - {B} social arguments", args.Length);
        logger.LogInformation("  - {C} argument links", links.Length);
        logger.LogInformation("  - 1 argument chain");
        logger.LogInformation("  - 1 worldview");
        logger.LogInformation("  - 1 debate room");
        logger.LogInformation("  - 1 user reputation");
        logger.LogInformation("  - 1 epistemic profile");
    }
}