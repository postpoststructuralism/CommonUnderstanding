namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Single source of truth for all badge definitions.
/// Used by BadgeAwardService (to know what to award),
/// ReputationController (to return badge details), and
/// Frontend (to render badge names, descriptions, tiers).
/// </summary>
public static class BadgeRegistry
{
    public record BadgeDefinition(string Id, string Name, string Description, string Tier);

    public static readonly Dictionary<string, BadgeDefinition> All = new()
    {
        // ── Onboarding (Bronze) ──────────────────────────────────────────────
        ["first_argument"] = new("first_argument", "First Voice", "Published your first public argument", "Bronze"),
        ["first_upvote"] = new("first_upvote", "Epistemic Validation", "Upvoted someone else's argument", "Bronze"),
        ["first_bridge"] = new("first_bridge", "Nexus Point", "Connected two opposing arguments with a qualifying link", "Bronze"),
        ["profile_complete"] = new("profile_complete", "Full Picture", "Completed your belief profile", "Bronze"),
        ["first_chain"] = new("first_chain", "First Chain", "Built your first reasoning chain", "Bronze"),

        // ── Engagement (Bronze → Silver) ─────────────────────────────────────
        ["streak_3"] = new("streak_3", "Warm Streak", "3 consecutive days of activity", "Bronze"),
        ["streak_7"] = new("streak_7", "Weekly Regular", "7 consecutive days of activity", "Bronze"),
        ["streak_30"] = new("streak_30", "Monthly Devotion", "30 consecutive days of activity", "Silver"),
        ["streak_100"] = new("streak_100", "Century Mark", "100 consecutive days of activity", "Silver"),
        ["voter_50"] = new("voter_50", "Active Citizen", "Cast 50 votes", "Bronze"),
        ["voter_500"] = new("voter_500", "Voice of the People", "Cast 500 votes", "Silver"),
        ["commenter_10"] = new("commenter_10", "Conversationalist", "Posted 10 follow-up replies", "Bronze"),
        ["commenter_50"] = new("commenter_50", "Dialogue Master", "Posted 50 follow-up replies", "Silver"),

        // ── Quality (Silver → Gold) ──────────────────────────────────────────
        ["top_argument_25"] = new("top_argument_25", "Respected Voice", "An argument reached 25 upvotes", "Silver"),
        ["top_argument_100"] = new("top_argument_100", "Influential", "An argument reached 100 upvotes", "Gold"),
        ["top_argument_500"] = new("top_argument_500", "Thought Leader", "An argument reached 500 upvotes", "Gold"),
        ["wilson_champion"] = new("wilson_champion", "Quality Champion", "3 arguments with Wilson score ≥ 0.85", "Silver"),
        ["fallacy_free_5"] = new("fallacy_free_5", "Clear Thinker", "5 arguments validated with no fallacies", "Silver"),
        ["fallacy_free_25"] = new("fallacy_free_25", "Rigorous Reasoner", "25 fallacy-free arguments", "Gold"),

        // ── Bridge-Building (Silver → Platinum) ⭐ CORE ──────────────────────
        ["bridge_1"] = new("bridge_1", "Paradigm Shift", "Created 1 resolution that resolves a contradiction", "Silver"),
        ["bridge_5"] = new("bridge_5", "Systemic Resolver", "Created 5 resolutions", "Gold"),
        ["bridge_25"] = new("bridge_25", "Architect of Truth", "Created 25 resolutions", "Gold"),
        ["bridge_100"] = new("bridge_100", "Graph Sovereign", "Created 100 resolutions", "Platinum"),
        ["convergence_catalyst"] = new("convergence_catalyst", "Matrix Catalyst", "Helped two users discover shared ground", "Silver"),
        ["convergence_10"] = new("convergence_10", "Matchmaker", "Catalyzed 10 convergence discoveries", "Gold"),
        ["harmony_spotter"] = new("harmony_spotter", "Premise Lock", "First time an AI-detected premise lock involves your argument", "Silver"),
        ["cross_aisle_voter"] = new("cross_aisle_voter", "Intellectual Omnivore", "Upvoted arguments from 10 different worldview clusters", "Silver"),
        ["cross_aisle_50"] = new("cross_aisle_50", "Bipartisan Spirit", "Upvoted arguments from 50 different worldview clusters", "Gold"),
        ["changed_mind"] = new("changed_mind", "Conqueror of Conviction", "5 users marked 'Changed My View' on your arguments", "Silver"),
        ["changed_mind_25"] = new("changed_mind_25", "Unassailable Reality", "25 'Changed My View' rationales received", "Gold"),

        // ── Evidence & Epistemic (Silver → Gold) ─────────────────────────────
        ["evidence_t1"] = new("evidence_t1", "Gold Standard", "Cited a T1 (meta-analysis) evidence source", "Silver"),
        ["evidence_t1_5"] = new("evidence_t1_5", "Evidence Scholar", "Cited 5 T1 sources", "Gold"),
        ["evidence_diverse"] = new("evidence_diverse", "Well-Rounded", "Cited evidence from 3+ different tiers", "Silver"),
        ["epistemic_expert"] = new("epistemic_expert", "Domain Expert", "Reached epistemic score 4.0 in any domain", "Silver"),
        ["epistemic_master"] = new("epistemic_master", "Master Reasoner", "Reached epistemic score 4.5 in 3+ domains", "Gold"),

        // ── Community Recognition (Gold → Platinum) ──────────────────────────
        ["community_pick"] = new("community_pick", "Community Pick", "An argument was featured by moderators", "Gold"),
        ["debate_champion"] = new("debate_champion", "Debate Champion", "Won a structured debate", "Gold"),
        ["consensus_builder"] = new("consensus_builder", "Consensus Builder", "Authored a resolution that 10+ users endorsed", "Platinum"),
        ["rising_star"] = new("rising_star", "Rising Star", "Gained 500 XP within first 30 days", "Silver"),
        ["elder"] = new("elder", "Platform Elder", "Active for 365+ days with 10,000+ XP", "Platinum"),

        // ── Hidden / Easter Egg ──────────────────────────────────────────────
        ["night_owl"] = new("night_owl", "Night Owl", "Posted 5 arguments between midnight-4am", "Bronze"),
        ["early_bird"] = new("early_bird", "Early Bird", "Posted 5 arguments between 5am-8am", "Bronze"),
        ["century_club"] = new("century_club", "Century Club", "100 arguments posted", "Silver"),
        ["millennium_club"] = new("millennium_club", "Millennium Club", "1,000 arguments posted", "Gold"),
    };

    public static BadgeDefinition? Get(string id) =>
        All.TryGetValue(id, out var def) ? def : null;

    /// <summary>Returns all badge IDs grouped by tier.</summary>
    public static ILookup<string, string> ByTier =>
        All.Values.ToLookup(b => b.Tier, b => b.Id);
}