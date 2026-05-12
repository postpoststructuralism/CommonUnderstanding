# User Belief System Comparison Feature

## Overview

This feature allows users to compare their **discovered belief profile** (generated through adaptive questioning) with **canonical belief systems** from the knowledge base (Buddhism, Stoicism, Existentialism, etc.).

---

## How It Works

### Step 1: User Discovers Their Beliefs

```
User answers questions ? AI analyzes responses ? Bayesian inference builds profile
  ?
BeliefSnapshot created with:
  - Values (ranked by importance)
  - Moral Foundations scores (6 dimensions)
  - Belief Dimensions (position + confidence)
  - Overall confidence level
```

### Step 2: Comparison to Canonical Systems

```
User clicks "Compare to Belief Systems"
  ?
System compares user's BeliefSnapshot to all CanonicalBeliefSystems
  ?
For each system, calculates:
  - Shared Values (text matching)
  - Moral Foundation Alignment (distance-based)
  - Dimensional Alignment (if dimensional data available)
  ?
Returns ranked list of matches
```

### Step 3: View Results

```
Top 20 matches displayed with:
  - Overall match percentage (0-100%)
  - Shared values highlighted
  - Moral foundation alignment bars
  - Key differences noted
  - Link to learn more about each system
```

---

## Match Calculation Algorithm

### Overall Match Percentage (0-100)

```
Total Score = Values Match (30 pts) + Moral Foundations (40 pts) + Dimensions (30 pts)
```

#### 1. Values Match (0-30 points)

```csharp
Shared Values Count × 6 (capped at 30)

Example:
User values: "freedom", "justice", "compassion", "wisdom", "community"
System keywords: "freedom", "justice", "equality", "duty"
Shared: "freedom", "justice" = 2 values
Score: 2 × 6 = 12 points
```

**Value Extraction from Systems:**
- Parse core principles for keywords
- Map synonyms (e.g., "liberty" ? "freedom")
- Filter short words (< 5 characters)

#### 2. Moral Foundations Match (0-40 points)

```csharp
Average alignment across 6 foundations × 40

For each foundation:
  Alignment = 1.0 - |UserScore - SystemScore| / 10.0

Example:
User Care: 8.2, System Care: 7.5
Alignment = 1.0 - |8.2 - 7.5| / 10.0 = 1.0 - 0.07 = 0.93 (93%)

Average all 6 foundations ? 0.75 (75%)
Score: 0.75 × 40 = 30 points
```

**Moral Foundation Inference for Systems:**
If system doesn't have explicit MF scores, infer from text:
- **Care**: Count mentions of "compassion", "kindness", "love", "mercy"
- **Fairness**: Count "justice", "equality", "rights"
- **Loyalty**: Count "community", "solidarity", "tradition"
- **Authority**: Count "hierarchy", "order", "discipline"
- **Sanctity**: Count "sacred", "holy", "pure", "divine"
- **Liberty**: Count "freedom", "autonomy", "independence"

#### 3. Dimensional Match (0-30 points)

```csharp
Average dimensional similarity × 30

For each shared dimension:
  Distance = |UserPosition - SystemPosition|
  Similarity = 1.0 - (Distance / 2.0)

Example:
User "political-economic": 0.6
System "political-economic": 0.3
Distance = |0.6 - 0.3| = 0.3
Similarity = 1.0 - (0.3 / 2.0) = 0.85 (85%)

Average all shared dimensions ? 0.70
Score: 0.70 × 30 = 21 points
```

**Total Example:**
```
Values: 12 points
Moral Foundations: 30 points
Dimensions: 21 points
Total: 63% match
```

---

## Match Tiers

| Percentage | Tier | Meaning |
|------------|------|---------|
| 70-100% | **High Match** | Strong philosophical alignment, core values overlap significantly |
| 50-69% | **Moderate Match** | Meaningful similarities, some shared principles |
| 30-49% | **Low Match** | Limited overlap, different priorities |
| 0-29% | **Minimal Match** | Fundamentally different worldviews |

---

## User Interface

### Compare to Canonical Page

**Layout:**
```
???????????????????????????????????????????????????
? Your Belief Profile Summary                     ?
? - 15 interactions completed                     ?
? - 68% confidence                                ?
? - Top values: Freedom, Justice, Compassion      ?
???????????????????????????????????????????????????

???????????????????????  ???????????????????????
? Stoicism            ?  ? Buddhism            ?
? 78% Match ?         ?  ? 72% Match ?         ?
?                     ?  ?                     ?
? Shared:             ?  ? Shared:             ?
? • wisdom            ?  ? • compassion        ?
? • duty              ?  ? • wisdom            ?
?                     ?  ?                     ?
? MF Alignment:       ?  ? MF Alignment:       ?
? Authority: 90%      ?  ? Care: 95%           ?
? Liberty: 85%        ?  ? Liberty: 88%        ?
?                     ?  ?                     ?
? [Learn More]        ?  ? [Learn More]        ?
???????????????????????  ???????????????????????
```

### Profile Page Addition

**New Button:**
```
[Continue Discovery] [Compare to Belief Systems ?] [View Evolution] [View Responses]
```

---

## Implementation Details

### Service Method: `CompareUserToCanonicalSystems()`

**Location:** `BeliefSystemKnowledgeBase.cs`

```csharp
public List<UserBeliefSystemMatch> CompareUserToCanonicalSystems(
    BeliefSnapshot userProfile, 
    int topN = 10)
{
    var matches = new List<UserBeliefSystemMatch>();

    foreach (var system in _allSystems)
    {
        var match = CompareUserToSystem(userProfile, system);
        matches.Add(match);
    }

    return matches
        .OrderByDescending(m => m.OverallMatchPercentage)
        .Take(topN)
        .ToList();
}
```

### Controller Action: `CompareToCanonical()`

**Location:** `DiscoveryController.cs`

```csharp
public IActionResult CompareToCanonical()
{
    // 1. Get user's profile from cookie
    var profile = _profileStore.GetProfile(profileId);
    
    // 2. Validate minimum data (5+ interactions)
    if (profile.InteractionCount < 5)
    {
        return RedirectToAction("Question");
    }
    
    // 3. Get top 20 matches
    var matches = _knowledgeBase.CompareUserToCanonicalSystems(
        profile.CurrentBeliefSnapshot, 
        topN: 20
    );
    
    // 4. Render view
    return View(matches);
}
```

### View: `CompareToCanonical.cshtml`

**Location:** `Views/Discovery/CompareToCanonical.cshtml`

**Features:**
- Card layout for each match
- Color-coded match percentage (green > 70%, yellow > 50%, gray < 50%)
- Expandable sections for shared values, MF alignment, differences
- Link to canonical system detail page
- Progress bars for moral foundation alignment
- Responsive grid (2 columns on desktop, 1 on mobile)

---

## Data Model

### UserBeliefSystemMatch

```csharp
public class UserBeliefSystemMatch
{
    public string SystemId { get; set; }
    public string SystemName { get; set; }
    public string SystemSlug { get; set; }
    public string SystemCategory { get; set; }
    public string SystemCulture { get; set; }
    public string SystemEra { get; set; }
    
    public double OverallMatchPercentage { get; set; } // 0-100
    
    public List<string> SharedValues { get; set; }
    public List<string> KeyDifferences { get; set; }
    
    public Dictionary<string, double> MoralFoundationAlignment { get; set; }
    public Dictionary<string, double> DimensionalAlignment { get; set; }
    
    public string MatchExplanation { get; set; }
}
```

### CanonicalBeliefSystem (Enhanced)

**Existing fields:**
- Id, Name, Slug, Category, Culture, Era
- Description, Sources, CorePrinciples
- Profile (BeliefSnapshot with dimensions/values)
- CreationMyth, HistoricalContext
- NotableFigures, Regions, RelatedSystems

**Profile is now used for comparison:**
- If system has `Profile.MoralFoundations` ? direct comparison
- If not ? infer from CorePrinciples text
- If system has `Profile.Dimensions` ? dimensional comparison
- If not ? skip dimensional matching

---

## Usage Flow

### User Journey

```
1. Start Discovery
   ?
2. Answer 5+ questions
   ?
3. View Profile
   ?
4. Click "Compare to Belief Systems"
   ?
5. See ranked matches with explanations
   ?
6. Click "Learn More" on a match
   ?
7. Read full description of canonical system
   ?
8. Optionally return to continue discovery
```

### Example Session

**User Profile After 15 Questions:**
```
Values: Freedom (9.2), Justice (8.7), Compassion (8.1)
Moral Foundations:
  - Liberty: 8.5
  - Fairness: 8.2
  - Care: 7.8
  - Authority: 4.2
  - Loyalty: 5.1
  - Sanctity: 3.9
Confidence: 68%
```

**Top Matches:**

1. **Secular Humanism (78%)**
   - Shared: freedom, justice, compassion, reason
   - High Liberty (85%), High Fairness (90%)
   - Low Authority (95%)

2. **Stoicism (72%)**
   - Shared: wisdom, duty, virtue
   - High Liberty (80%), Moderate Care (75%)
   - Some Authority emphasis (60%)

3. **Buddhism (68%)**
   - Shared: compassion, wisdom
   - High Care (92%), High Liberty (85%)
   - Lower Fairness focus (70%)

---

## Future Enhancements

### Short Term

1. **AI-Generated Match Narratives**
   ```csharp
   // Use Semantic Kernel to generate personalized explanations
   var narrative = await GenerateMatchNarrativeAsync(userProfile, system, matchScore);
   ```

2. **Historical Context**
   - Show how historical figures in matched systems might view user's beliefs
   - Timeline showing evolution of matched belief systems

3. **Reading Recommendations**
   - Suggest books/texts from matched systems
   - Ranked by match percentage

### Medium Term

4. **Hybrid Profiles**
   - "You're 60% Stoic, 30% Buddhist, 10% Existentialist"
   - Visual blend chart

5. **Contradiction Highlighting**
   - "Your high compassion aligns with Buddhism, but your individualism differs"
   - Nuanced analysis of where you diverge from top matches

6. **Peer Comparison**
   - "Others with similar profiles also matched highly with..."
   - Cluster analysis of user types

### Long Term

7. **Interactive Exploration**
   - "Answer 5 more questions to clarify your stance on X"
   - Targeted questions to resolve ambiguous matches

8. **Belief Journey**
   - Track how matches change over time
   - "You started closer to X but evolved toward Y"

9. **Community Features**
   - Find other users with similar belief profiles
   - Discussion forums grouped by dominant matches

---

## Technical Considerations

### Performance

**Current:**
- Compares against all systems in memory (fast for <100 systems)
- O(n) where n = number of canonical systems
- Typical: ~20-50 systems = <100ms

**Optimization if needed:**
- Pre-compute system embeddings
- Use vector similarity search
- Cache user profile signatures

### Accuracy

**Depends on:**
1. **User data quality**
   - More interactions = better confidence
   - Minimum 5, optimal 15+

2. **System data quality**
   - Comprehensive core principles
   - Accurate moral foundation profiles
   - Dimensional data (if available)

3. **Matching algorithm**
   - Current: heuristic-based
   - Future: ML-based similarity

### Edge Cases

1. **Low confidence profiles (<40%)**
   - Show warning: "Answer more questions for better accuracy"
   - Limit to top 5 matches only

2. **No significant matches (all <30%)**
   - Show: "Your worldview is unique!"
   - Suggest answering more questions
   - Highlight closest partial matches

3. **System without MF profile**
   - Infer from text (less accurate)
   - Note in match explanation: "Estimated alignment"

---

## Testing

### Manual Testing Checklist

- [ ] User with 0-4 questions ? Redirected to continue discovery
- [ ] User with 5+ questions ? See comparison page
- [ ] Top matches sorted by percentage (high to low)
- [ ] Shared values displayed correctly
- [ ] MF alignment bars render properly
- [ ] Key differences listed
- [ ] "Learn More" links work
- [ ] Responsive on mobile
- [ ] Profile summary accurate
- [ ] Back button returns to profile

### Unit Testing

```csharp
[Fact]
public void CompareUserToCanonicalSystems_ReturnsTopMatches()
{
    // Arrange
    var userProfile = CreateTestProfile();
    var knowledgeBase = new BeliefSystemKnowledgeBase();
    
    // Act
    var matches = knowledgeBase.CompareUserToCanonicalSystems(userProfile, 10);
    
    // Assert
    Assert.Equal(10, matches.Count);
    Assert.All(matches, m => Assert.InRange(m.OverallMatchPercentage, 0, 100));
    Assert.True(matches[0].OverallMatchPercentage >= matches[1].OverallMatchPercentage);
}
```

---

## Summary

**What Was Added:**

1. ? `CompareUserToCanonicalSystems()` method in `BeliefSystemKnowledgeBase`
2. ? `CompareUserToSystem()` private method with comprehensive matching
3. ? Value extraction and keyword matching
4. ? Moral foundation comparison (with inference for systems without explicit profiles)
5. ? Dimensional alignment calculation
6. ? Overall match percentage algorithm
7. ? `UserBeliefSystemMatch` data model
8. ? `CompareToCanonical()` controller action
9. ? `CompareToCanonical.cshtml` view with card-based UI
10. ? Button added to Profile page

**User Benefit:**

Users can now:
- See which established belief systems align with their discovered worldview
- Understand **why** they match (shared values, moral foundations)
- Explore **differences** and unique aspects
- Learn about philosophical traditions that resonate with them
- Continue discovery with context of how their beliefs evolve relative to known systems

**This bridges personal discovery with collective wisdom! ??**
