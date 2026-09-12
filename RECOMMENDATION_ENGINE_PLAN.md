# AI-Driven Feed Recommendation Engine — Plan

## 1. Context

Today, `SocialViewController.Feed()` orders published arguments by:

```csharp
.OrderByDescending(a => a.WilsonScore)
.ThenByDescending(a => a.UpvoteCount - a.DownvoteCount)
```

This is a single-objective, non-personalized popularity ranker (Wilson lower-bound
confidence interval on upvote ratio, tie-broken by net votes). It has no concept of
per-user interest, growth/fit, collective value, or user control. This plan replaces
it with a multi-lane, customizable recommendation pipeline while preserving
WilsonScore as an input signal rather than discarding it.

## 2. Goals

1. Mix three objectives per user, per feed load:
   - **Interest** — what the user already engages with.
   - **Growth-fit** — what's good for *this* person given their expertise/experience
     (a "stretch" without being irrelevant).
   - **Collective** — what helps the evolution of shared understanding across the
     whole user base (bridging content, contradiction/synthesis density, GC policy fit).
2. Make the mix fully user-customizable (sliders/toggles, saved presets).
3. Keep it explainable — every card shows why it's in the feed.
4. Keep it learnable — engagement events feed back into scoring over time.

## 3. Pipeline

```
Candidate Generation
      -> Per-lane Scoring (Interest / Growth / Collective)
      -> Weighted Blend (user-controlled weights)
      -> Explore/Exploit layer (bandit)
      -> Diversity Re-rank (MMR)
      -> Serve + Explain tags
      -> Log impressions/engagement -> retrain scorers
```

### 3.1 Candidate Generation
Pull a wider pool than will be displayed:
- Last N days of published arguments (recency pool)
- High Understanding-Graph centrality / contradiction-adjacent nodes
- A small uniform-random sample (cold-start / serendipity floor)
- Exclude items the user has already seen/voted on in the last session unless
  "resurface" is enabled in preferences.

### 3.2 Per-Lane Scoring

**IInterestScorer**
- Cosine similarity between the argument's embedding and the user's rolling
  interest vector (EMA over past likes/comments/follows/dwell time).
- Boost for followed tags/topics and followed authors.

**IGrowthScorer**
- Compare the argument's topic/value profile against the user's expertise
  profile (declared domain + inferred from contribution history).
- Reward propositions the user hasn't engaged with (novelty) but that sit within
  one "hop" of their known domains — not fully random, but a genuine stretch.
- Penalize pure duplication of things the user has already mastered/argued.

**ICollectiveScorer**
- WilsonScore as a quality floor (filters out low-confidence/low-engagement noise).
- **Bridging score**: cluster users by Schwartz value-vector similarity (or by
  voting-pattern matrix factorization, Polis/Community-Notes style). Score an
  argument higher if its positive engagement spans multiple clusters rather than
  concentrating in one — i.e. it helps people who disagree elsewhere find common
  ground here.
- Contradiction/synthesis density from the Understanding Graph — arguments that
  sit at unresolved contradiction nodes, or that a synthesis agent has flagged as
  load-bearing, score higher.
- Departmental/GC policy-fit score (existing fit-scoring output), where relevant.

### 3.3 Weighted Blend

```
Score = w_interest * Interest + w_growth * Growth + w_collective * Collective
w_interest + w_growth + w_collective = 1
```

Default weights: Interest 0.5, Growth 0.25, Collective 0.25. These three weights
are exactly what the user-facing sliders control.

### 3.4 Explore/Exploit

Use Thompson sampling (or epsilon-greedy to start) over the blended score's
uncertainty so under-voted/newer arguments occasionally surface instead of only
reinforcing already-popular items. Track a Beta(a,b) posterior per argument per
lane, updated from impression/engagement outcomes.

### 3.5 Diversity Re-rank (MMR)

After blend + explore produces a ranked candidate list, apply Maximal Marginal
Relevance so consecutive cards aren't near-duplicates on topic or value-profile:

```
MMR(d) = λ * Score(d) - (1-λ) * max_sim(d, already_selected)
```

λ tunable (default ~0.7).

## 4. User Customization Surface

Add a "Tune your feed" panel on `SocialView/Feed`:

- **Interest / Growth / Collective sliders** — map directly to blend weights.
- **Adventure slider** — % of fully non-personalized/random items mixed in.
- **Challenge me control** — frequency + optional topic scope for deliberately
  contrasting-value arguments.
- **Recency weighting slider** — recent vs. long-run history for Interest lane.
- **Source-type toggles** — expert-authored, cross-department, evidence-cited vs.
  opinion-only.
- **Explain tag on every card**: "Interest" / "Growth" / "Collective" / "Explore",
  plus a one-line reason ("Because you engaged with UBI arguments" / "Bridges two
  opposing value clusters" / "Stretches into a new policy area").
- **Named presets**: "Focus," "Balanced," "Bridge-builder" — bundles of the above,
  saved per user.

## 5. Data Model Additions (EF Core)

```csharp
public class UserFeedPreferences
{
    public int UserId { get; set; }
    public double InterestWeight { get; set; } = 0.5;
    public double GrowthWeight { get; set; } = 0.25;
    public double CollectiveWeight { get; set; } = 0.25;
    public double AdventureRate { get; set; } = 0.1;
    public double ChallengeRate { get; set; } = 0.1;
    public double RecencyBias { get; set; } = 0.5;
    public string? SourceTypeFiltersJson { get; set; }
    public string? ActivePresetName { get; set; }
}

public class FeedImpressionEvent
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int ArgumentId { get; set; }
    public double InterestScoreAtServe { get; set; }
    public double GrowthScoreAtServe { get; set; }
    public double CollectiveScoreAtServe { get; set; }
    public string Lane { get; set; } = default!; // Interest/Growth/Collective/Explore
    public bool Clicked { get; set; }
    public bool Voted { get; set; }
    public bool Commented { get; set; }
    public int DwellMs { get; set; }
    public DateTime ServedAt { get; set; }
}
```

## 6. Service Layer

```
IFeedRankingService
  - GetFeedAsync(userId, page, pageSize) -> IEnumerable<RankedArgumentCard>

IInterestScorer / IGrowthScorer / ICollectiveScorer
  - ScoreAsync(userId, IEnumerable<Argument> candidates) -> Dictionary<int,double>

IBridgingClusterService
  - GetUserClusterVector(userId) -> double[]   // from Schwartz classifier output
  - GetArgumentBridgingScore(argumentId) -> double

IFeedBanditService
  - SelectAndUpdate(candidates, scores) -> ranked list with explore flags
```

`SocialViewController.Feed()` changes to:
1. Load `UserFeedPreferences` for the current user (create defaults if none).
2. Call `IFeedRankingService.GetFeedAsync(...)` instead of the raw
   `OrderByDescending(WilsonScore)` query.
3. Pass preferences into the view for the "Tune your feed" panel.
4. Log a `FeedImpressionEvent` per card actually rendered.

## 7. Rollout Plan

- **Phase 1**: Ship Interest + Collective (WilsonScore-based) lanes only, no
  sliders yet, to validate the pipeline plumbing against the current feed
  behavior (should look similar to today's output with weights 0.7/0/0.3).
- **Phase 2**: Add Growth lane (needs Schwartz value profiles per user, already
  available from the classifier) and the bridging-cluster computation.
- **Phase 3**: Ship the customization panel and impression logging.
- **Phase 4**: Turn on the bandit layer and start retraining lane weights/priors
  from real engagement data; A/B test default weight presets.

## 8. Evaluation Metrics

- Engagement: click-through, dwell time, comment/vote rate per lane.
- Diversity: average pairwise embedding distance within a served page.
- Bridging health: fraction of served arguments with cross-cluster positive
  engagement (the metric that most directly maps to "evolution of human
  understanding").
- Control usage: % of users who adjust sliders away from defaults, and whether
  doing so increases session length/return rate (validates the customization
  investment).
