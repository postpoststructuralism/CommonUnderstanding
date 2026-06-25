using CommonUnderstanding.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace CommonUnderstanding.Services;

/// <summary>
/// Generates AI-powered narrative summaries of a user's belief profile,
/// synthesizing dimensions, values, moral foundations, and canonical matches
/// into an engaging, readable summary.
/// </summary>
public class WorldviewSummaryService
{
    private readonly IChatCompletionService _chatService;
    private readonly BeliefSystemKnowledgeBase _knowledgeBase;
    private readonly ILogger<WorldviewSummaryService> _logger;

    public WorldviewSummaryService(
        IChatCompletionService chatService,
        BeliefSystemKnowledgeBase knowledgeBase,
        ILogger<WorldviewSummaryService> logger)
    {
        _chatService = chatService;
        _knowledgeBase = knowledgeBase;
        _logger = logger;
    }

    /// <summary>
    /// Generate an AI-powered narrative summary of the user's belief profile.
    /// Returns several substantial paragraphs highlighting key observations and
    /// positioning the user in the moral-socio-political-religious universe.
    /// </summary>
    public async Task<string> GenerateSummaryAsync(BeliefSnapshot snapshot)
    {
        if (snapshot.InteractionCount < 3)
        {
            return "Answer a few more questions to unlock your personalized worldview summary.";
        }

        try
        {
            var prompt = BuildSummaryPrompt(snapshot);
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(
                "You are an expert worldview analyst and cultural anthropologist with deep knowledge of " +
                "philosophy, political theory, comparative religion, and moral psychology. Your role is to " +
                "help people understand where their unique belief system fits within the vast landscape of " +
                "human thought. You write with the depth of a seasoned essayist and the warmth of a trusted " +
                "mentor. Your analysis should be specific, nuanced, and grounded in the data provided. " +
                "Avoid vague generalities — name specific traditions, thinkers, and concepts when relevant. " +
                "Write 3-4 substantial paragraphs (at least 300 words total). " +
                "Do NOT use markdown headings, bullet points, or lists. Use plain paragraphs only.");
            chatHistory.AddUserMessage(prompt);

            var result = await _chatService.GetChatMessageContentsAsync(chatHistory);
            var summary = result.FirstOrDefault()?.Content?.Trim();

            if (!string.IsNullOrWhiteSpace(summary) && summary.Length >= 150)
            {
                _logger.LogInformation("Generated AI worldview summary for user {UserId} ({Length} chars)",
                    snapshot.UserId, summary.Length);
                return summary;
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                _logger.LogWarning("AI summary too short ({Length} chars), retrying with stronger prompt",
                    summary.Length);
                // Retry once with stronger instructions
                chatHistory.AddUserMessage(
                    "That was too brief. Please expand significantly. Write 3-4 rich, detailed paragraphs " +
                    "(at least 300 words) that dive deep into the patterns, tensions, and affinities in this " +
                    "profile. Be specific about what these patterns mean and which intellectual or spiritual " +
                    "traditions they echo.");
                var retryResult = await _chatService.GetChatMessageContentsAsync(chatHistory);
                var retrySummary = retryResult.FirstOrDefault()?.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(retrySummary) && retrySummary.Length >= 150)
                    return retrySummary;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI summary generation failed for user {UserId}, falling back to template",
                snapshot.UserId);
        }

        return GenerateFallbackSummary(snapshot);
    }

    private string BuildSummaryPrompt(BeliefSnapshot snapshot)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("I need you to write a rich, insightful narrative summary of a person's worldview ");
        sb.AppendLine("based on their responses to belief-discovery questions. Here is their profile data:");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine($"PROFILE SUMMARY: {snapshot.InteractionCount} questions answered");
        sb.AppendLine($"Model confidence: {snapshot.OverallConfidence:P0}");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();

        // ── Dimensions with semantic interpretation ──
        var significantDimensions = snapshot.Dimensions
            .Where(d => d.Confidence > 0.15 && d.Position.HasValue)
            .OrderByDescending(d => d.Confidence)
            .ToList();

        if (significantDimensions.Any())
        {
            sb.AppendLine("BELIEF DIMENSIONS (position on each spectrum, with confidence):");
            sb.AppendLine();
            foreach (var dim in significantDimensions)
            {
                var interpretation = InterpretDimension(dim);
                sb.AppendLine($"  • {dim.Name} ({dim.Category})");
                sb.AppendLine($"    Position: {dim.Position:F2} on -1 to +1 scale");
                sb.AppendLine($"    Meaning: {interpretation}");
                sb.AppendLine($"    Confidence: {dim.Confidence:P0} | Evidence from {dim.SampleSize} responses");
                sb.AppendLine();
            }
        }

        // ── Values with richer context ──
        var topValues = snapshot.Values
            .OrderByDescending(v => v.Confidence)
            .Take(6)
            .ToList();

        if (topValues.Any())
        {
            sb.AppendLine("CORE VALUES (what this person cares about most):");
            foreach (var v in topValues)
            {
                var strength = v.Confidence > 0.8 ? "dominant" : v.Confidence > 0.6 ? "strong" : v.Confidence > 0.4 ? "notable" : "emerging";
                sb.AppendLine($"  • {v.Name} — {strength} ({v.Confidence:P0} confidence, importance: {v.ImportanceScore:F1}/10)");
            }
            sb.AppendLine();
        }

        // ── Moral foundations with interpretation ──
        var mf = snapshot.MoralFoundations;
        if (mf != null)
        {
            sb.AppendLine("MORAL FOUNDATIONS (Haidt's framework, 0-10 scale — 5.0 is neutral/midpoint):");
            sb.AppendLine();
            sb.AppendLine("  IMPORTANT: Scores near 5.0 are NEUTRAL — do NOT describe them as \"high\" or \"low\".");
            sb.AppendLine("  Only scores ≥7.0 are genuinely high. Only scores ≤3.0 are genuinely low.");
            sb.AppendLine();
            AppendFoundation(sb, "Care/Harm", mf.Care.Score,
                "High (≥7) = prioritizes compassion, nurturing, protection of the vulnerable. Low (≤3) = values toughness, self-reliance, believes people should handle their own problems.");
            AppendFoundation(sb, "Fairness/Cheating", mf.Fairness.Score,
                "High (≥7) = strong sense of justice, rights, equality, and proportionality. Low (≤3) = pragmatic, accepts unequal outcomes as natural or deserved.");
            AppendFoundation(sb, "Loyalty/Betrayal", mf.Loyalty.Score,
                "High (≥7) = values group solidarity, patriotism, in-group commitment, team before self. Low (≤3) = cosmopolitan, skeptical of tribalism, prioritizes universal over group identity.");
            AppendFoundation(sb, "Authority/Subversion", mf.Authority.Score,
                "High (≥7) = respects hierarchy, tradition, established order, and legitimate authority. Low (≤3) = questions authority, values autonomy, skeptical of top-down power.");
            AppendFoundation(sb, "Sanctity/Degradation", mf.Sanctity.Score,
                "High (≥7) = values purity, sacredness, bodily/spiritual integrity, moral disgust at degradation. Low (≤3) = secular, pragmatic, sees morality in terms of harm/fairness rather than purity.");
            AppendFoundation(sb, "Liberty/Oppression", mf.Liberty.Score,
                "High (≥7) = fiercely values freedom, resists coercion, suspicious of concentrated power. Low (≤3) = accepts constraints on freedom for collective good, security, or order.");
            sb.AppendLine();
        }

        // ── Canonical matches with full context ──
        var matches = _knowledgeBase.CompareUserToCanonicalSystems(snapshot, topN: 5);
        if (matches.Any())
        {
            sb.AppendLine("NEAREST CANONICAL BELIEF SYSTEMS:");
            sb.AppendLine();
            foreach (var m in matches.Take(5))
            {
                var system = _knowledgeBase.GetByName(m.SystemName);
                sb.AppendLine($"  {m.OverallMatchPercentage:P0} match — {m.SystemName}");
                sb.AppendLine($"    Category: {m.SystemCategory} | Culture: {m.SystemCulture} | Era: {m.SystemEra}");
                if (system?.Description is { Length: > 0 } desc)
                    sb.AppendLine($"    Description: {desc}");
                if (system?.CorePrinciples is { Count: > 0 } principles)
                    sb.AppendLine($"    Core tenets: {string.Join("; ", principles.Take(5))}");
                if (system?.NotableFigures is { Count: > 0 } figures)
                    sb.AppendLine($"    Key figures: {string.Join(", ", figures.Take(5))}");
                if (m.SharedValues is { Count: > 0 } shared)
                    sb.AppendLine($"    Values you share: {string.Join(", ", shared.Take(5))}");
                if (m.KeyDifferences is { Count: > 0 } diffs)
                    sb.AppendLine($"    Where you differ: {string.Join(", ", diffs.Take(3))}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("GROUND RULES — READ CAREFULLY BEFORE WRITING:");
        sb.AppendLine();
        sb.AppendLine("  • ONLY describe patterns that are ACTUALLY PRESENT in the data above.");
        sb.AppendLine("  • If a moral foundation score is below 5.0/10, do NOT call it \"high\" or \"strong.\"");
        sb.AppendLine("  • If a score is above 5.0/10, do NOT call it \"low\" or \"weak.\"");
        sb.AppendLine("  • Scores near 5.0 (±1.0) are NEUTRAL — do not build narratives around them.");
        sb.AppendLine("  • Do NOT invent tensions or conflicts between values unless BOTH values are genuinely");
        sb.AppendLine("    high (≥7.0) AND genuinely point in opposite directions.");
        sb.AppendLine("  • If the profile is coherent and consistent, SAY SO. Don't fabricate drama.");
        sb.AppendLine("  • Before writing, mentally identify the 1-2 HIGHEST scores (≥7.0) and 1-2 LOWEST scores");
        sb.AppendLine("    (≤3.0). If there are no extreme scores, the story is one of MODERATION — tell that story.");
        sb.AppendLine("  • Do NOT use the word \"fierce\" unless a score is ≥8.0/10.");
        sb.AppendLine("  • Do NOT describe a score of 4.0-6.0 as \"high\" or \"strong\" — it is moderate/middling.");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("YOUR TASK:");
        sb.AppendLine();
        sb.AppendLine("Write 3-4 substantial paragraphs (at least 300 words total) that synthesize this data ");
        sb.AppendLine("into an HONEST, ACCURATE narrative portrait of this person's worldview. Your summary should:");
        sb.AppendLine();
        sb.AppendLine("1. OPEN with the most FACTUALLY striking pattern in their profile. What are their actual ");
        sb.AppendLine("   highest and lowest scores? Is there a genuine tension (both scores ≥7.0 in tension) or ");
        sb.AppendLine("   is the profile coherent? Don't force drama — if the profile is moderate and balanced, ");
        sb.AppendLine("   that IS the story.");
        sb.AppendLine();
        sb.AppendLine("2. MAP their position using ONLY what the data shows. Where do they sit on the key spectra? ");
        sb.AppendLine("   Use the moral foundation scores honestly — a 4.6 is below the midpoint, a 7.1 is elevated. ");
        sb.AppendLine("   Describe what IS there, not what would make a better story.");
        sb.AppendLine();
        sb.AppendLine("3. CONNECT them to the canonical traditions listed above. Which ones do they most resemble ");
        sb.AppendLine("   and why? What does that affinity reveal? If they match multiple traditions, explore what ");
        sb.AppendLine("   those traditions actually share (rather than inventing a conflict between them).");
        sb.AppendLine();
        sb.AppendLine("4. CLOSE with a warm, human reflection grounded in their actual scores — help them see their ");
        sb.AppendLine("   beliefs as a coherent perspective with echoes across history and culture.");
        sb.AppendLine();
        sb.AppendLine("CRITICAL: Be specific AND accurate. Name the traditions and values you see. Avoid phrases like ");
        sb.AppendLine("\"you have a unique worldview\" without explaining what makes it unique. Write like a thoughtful ");
        sb.AppendLine("essayist — insightful, precise, honest, and generous. Do NOT use markdown, headings, or bullet points.");
        sb.AppendLine("Output ONLY the summary paragraphs, separated by blank lines.");

        return sb.ToString();
    }

    private static void AppendFoundation(StringBuilder sb, string name, double score, string interpretation)
    {
        // Use a 0-10 scale since that's what the UI shows users
        var level = score >= 7.5 ? "very high" : score >= 6.0 ? "high" : score >= 4.5 ? "moderate" : score >= 3.0 ? "low" : "very low";
        sb.AppendLine($"  {name}: {score:F1}/10 ({level}) — {interpretation}");
    }

    private static string InterpretDimension(BeliefDimension dim)
    {
        // Provide semantic interpretation based on the dimension name and position
        var pos = dim.Position ?? 0;
        var name = dim.Name.ToLower();

        // Try to give a meaningful interpretation based on dimension name
        if (name.Contains("collectiv") || name.Contains("individual"))
            return pos < -0.3 ? "Leans collectivist — prioritizes group welfare over individual autonomy" :
                   pos > 0.3 ? "Leans individualist — prioritizes personal freedom and autonomy" :
                   "Balanced between collective and individual concerns";

        if (name.Contains("tradition") || name.Contains("progress"))
            return pos < -0.3 ? "Values tradition and established wisdom" :
                   pos > 0.3 ? "Values progress, innovation, and change" :
                   "Balanced between tradition and progress";

        if (name.Contains("spiritual") || name.Contains("religio") || name.Contains("sacred"))
            return pos < -0.3 ? "Strongly spiritual or religious orientation" :
                   pos > 0.3 ? "Secular or materialist orientation" :
                   "Moderate spiritual inclination";

        if (name.Contains("authorit") || name.Contains("libert"))
            return pos < -0.3 ? "Favors strong authority and order" :
                   pos > 0.3 ? "Favors maximum personal liberty" :
                   "Balanced on authority-liberty spectrum";

        if (name.Contains("econom") || name.Contains("market"))
            return pos < -0.3 ? "Favors regulation and redistribution" :
                   pos > 0.3 ? "Favors free markets and competition" :
                   "Moderate economic views";

        if (name.Contains("egalit") || name.Contains("hierarch"))
            return pos < -0.3 ? "Strongly egalitarian — values equality" :
                   pos > 0.3 ? "Accepts hierarchy as natural or beneficial" :
                   "Moderate on equality-hierarchy spectrum";

        if (name.Contains("ideal") || name.Contains("pragmat"))
            return pos < -0.3 ? "Principle-driven — values ideals over practical concerns" :
                   pos > 0.3 ? "Pragmatic — judges ideas by their real-world results" :
                   "Balances ideals with practicality";

        if (name.Contains("optimis") || name.Contains("pessim"))
            return pos < -0.3 ? "Tends toward pessimism about human nature" :
                   pos > 0.3 ? "Tends toward optimism about human nature" :
                   "Moderate outlook on human nature";

        if (name.Contains("universal") || name.Contains("particular"))
            return pos < -0.3 ? "Universalist — believes moral truths apply across cultures" :
                   pos > 0.3 ? "Particularist — believes context and culture shape morality" :
                   "Balanced between universal and particular";

        // Generic fallback
        return pos < -0.5 ? $"Strongly toward the {dim.Name} pole" :
               pos < -0.15 ? $"Moderately toward the {dim.Name} pole" :
               pos > 0.5 ? $"Strongly toward the opposite of {dim.Name}" :
               pos > 0.15 ? $"Moderately toward the opposite of {dim.Name}" :
               $"Near the center on {dim.Name}";
    }

    private string GenerateFallbackSummary(BeliefSnapshot snapshot)
    {
        var sb = new System.Text.StringBuilder();

        var topValues = snapshot.Values
            .OrderByDescending(v => v.Confidence)
            .Take(3)
            .Select(v => v.Name.ToLower())
            .ToList();

        var matches = _knowledgeBase.CompareUserToCanonicalSystems(snapshot, topN: 3);

        sb.Append("Based on your responses, your worldview shows a distinctive pattern. ");

        if (topValues.Any())
        {
            sb.Append($"Your thinking is strongly guided by values like {string.Join(", ", topValues)}. ");
        }

        if (matches.Any())
        {
            var top = matches.First();
            sb.Append($"Your perspective most closely aligns with {top.SystemName} " +
                       $"({top.OverallMatchPercentage:P0} match), ");
            if (matches.Count > 1)
            {
                sb.Append($"followed by {matches[1].SystemName} and {matches.Last().SystemName}. ");
            }
            else
            {
                sb.Append("suggesting a coherent and well-defined worldview. ");
            }
        }

        sb.Append("Continue exploring to refine this picture and discover deeper connections " +
                  "between your beliefs and the world's great traditions of thought.");

        return sb.ToString();
    }
}