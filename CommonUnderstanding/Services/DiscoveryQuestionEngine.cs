using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;
using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Generates adaptive questions and scenarios to discover user beliefs
/// </summary>
public class DiscoveryQuestionEngine
{
    private static readonly TimeSpan QuestionGenerationTimeout = TimeSpan.FromSeconds(15);

    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<DiscoveryQuestionEngine> _logger;

    public DiscoveryQuestionEngine(
        SemanticKernelService kernelService,
        ILogger<DiscoveryQuestionEngine> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Generate the next best question based on current knowledge and gaps
    /// </summary>
    public async Task<UserInteraction> GenerateNextQuestionAsync(UserProfile profile)
    {
        var kernel = _kernelService.GetKernel();
        
        var stage = DetermineStage(profile);
        
        // For initial survey, use predefined multiple choice questions
        if (stage == DiscoveryStage.Initial && profile.InteractionCount < 5)
        {
            return GenerateInitialSurveyQuestion(profile);
        }
        
        var uncertainAreas = profile.CurrentBeliefSnapshot?.Statistics.UncertainAreas ?? new List<string>();
        var previousQuestions = profile.Interactions.Select(i => i.Content.Question).ToList();

        var prompt = BuildQuestionPrompt(stage, uncertainAreas, previousQuestions, profile);

        _logger.LogInformation("Generating question for user {UserId} at stage {Stage}", profile.Id, stage);

        string questionData;
        try
        {
            var result = await InvokePromptWithTimeoutAsync(kernel, prompt, QuestionGenerationTimeout);
            questionData = result.ToString();
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("GenerateNextQuestionAsync timed out for user {UserId}; using scale fallback", profile.Id);
            return GenerateScaleQuestion(profile);
        }

        _logger.LogInformation("Successfully generated question for user {UserId}", profile.Id);

        return ParseQuestionResponse(questionData, profile.Id);
    }

    /// <summary>
    /// Generate a moral dilemma to test specific dimensions
    /// </summary>
    public async Task<UserInteraction> GenerateMoralDilemmaAsync(
        UserProfile profile, 
        List<string> targetDimensions)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $$$"""
        Always respond in English.
        You are a moral psychologist designing dilemmas to understand someone's ethical framework.
        
        Create a compelling moral dilemma that will reveal beliefs about: {{{string.Join(", ", targetDimensions)}}}
        
        The dilemma should:
        1. Present a realistic, relatable scenario
        2. Have no clear "right" answer
        3. Force a choice between competing values
        4. Elicit genuine emotional and moral reasoning
        5. Be culturally sensitive and thought-provoking
        
        Current understanding of this person:
        {{{profile.CurrentBeliefSnapshot?.NarrativeSummary ?? "Unknown - this is an early interaction"}}}
        
        Format your response as:
        SCENARIO: [Describe the situation in 2-3 paragraphs]
        QUESTION: [The specific choice they must make]
        FOLLOW_UP: [A follow-up question asking them to explain their reasoning]
        """;

        _logger.LogInformation("Generating moral dilemma for dimensions: {Dimensions}", string.Join(", ", targetDimensions));
        
        string dilemmaText;
        try
        {
            var result = await InvokePromptWithTimeoutAsync(kernel, prompt, QuestionGenerationTimeout);
            dilemmaText = result.ToString();
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("GenerateMoralDilemmaAsync timed out for user {UserId}; using fallback dilemma", profile.Id);
            dilemmaText = "SCENARIO: A town can either preserve a long-standing local tradition or replace it with a policy that may improve fairness for newcomers.\nQUESTION: Which option should the town choose?\nFOLLOW_UP: What value mattered most in your choice?";
        }

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.MoralDilemma,
            Content = ParseDilemmaContent(dilemmaText),
            TargetedDimensions = targetDimensions
        };
    }

    /// <summary>
    /// Generate a scenario designed to elicit emotional response
    /// </summary>
    public async Task<UserInteraction> GenerateEmotionalScenarioAsync(
        UserProfile profile,
        string targetEmotion)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $$$"""
        Create a brief scenario designed to elicit {{{targetEmotion}}} and reveal underlying values.
        
        The scenario should:
        1. Be realistic and relatable
        2. Naturally evoke {{{targetEmotion}}}
        3. Require the person to make a judgment or decision
        4. Reveal something about their values and priorities
        
        Keep it under 100 words. End with a question asking what they would do or how they feel about it.
        """;

        _logger.LogInformation("Generating emotional scenario targeting: {Emotion}", targetEmotion);
        
        string questionText;
        try
        {
            var result = await InvokePromptWithTimeoutAsync(kernel, prompt, QuestionGenerationTimeout);
            questionText = result.ToString();
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("GenerateEmotionalScenarioAsync timed out for user {UserId}; using fallback prompt", profile.Id);
            questionText = $"Describe a situation that would make you feel {targetEmotion}. What would your reaction reveal about what you value most?";
        }
        
        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.EmotionalPrompt,
            Content = new InteractionContent
            {
                Question = questionText,
                Format = InteractionFormat.OpenText
            }
        };
    }

    private async Task<FunctionResult> InvokePromptWithTimeoutAsync(Kernel kernel, string prompt, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        try
        {
            return await kernel.InvokePromptAsync(prompt, cancellationToken: timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Discovery question prompt timed out after {Seconds}s", timeout.TotalSeconds);
            throw new TimeoutException($"Discovery question generation timed out after {timeout.TotalSeconds} seconds.");
        }
    }

    /// <summary>
    /// Generate a scale question for a specific dimension
    /// </summary>
    /// <summary>
    /// Pick a scale question the user has not yet seen.
    /// Optionally prioritise a specific <paramref name="preferDimension"/> (e.g. the one
    /// with highest uncertainty), but always falls back to the full unseen pool
    /// so the question is never repeated.
    /// </summary>
    public UserInteraction GenerateScaleQuestion(
        UserProfile profile,
        string? preferDimension = null)
    {
        // Each entry: (question, minLabel, maxLabel, dimensions[])
        var pool = new List<(string Question, string MinLabel, string MaxLabel, List<string> Dimensions)>
        {
            (
                "How much should individuals be free to act as they choose versus being bound by collective responsibilities?",
                "Individual freedom above all",
                "Collective good above all",
                new List<string> { "political-economic", "individualism-collectivism" }
            ),
            (
                "Should society prioritise preserving proven traditions or actively pursuing progress and reform?",
                "Preserve tradition",
                "Embrace change",
                new List<string> { "political-social", "tradition", "change-orientation" }
            ),
            (
                "When judging whether an action is right, how much do intentions matter versus outcomes?",
                "Intentions matter most",
                "Outcomes matter most",
                new List<string> { "moral-reasoning", "consequentialism-deontology" }
            ),
            (
                "How important do you think respect for established authority and hierarchy is in a healthy society?",
                "Authority undermines freedom",
                "Authority is essential",
                new List<string> { "authority" }
            ),
            (
                "To what extent do you believe there are meaningful spiritual or transcendent forces beyond the material world?",
                "Pure materialism — nothing beyond the physical",
                "Strong spiritual or transcendent reality",
                new List<string> { "spirituality", "religion" }
            ),
            (
                "In general, do you think people are fundamentally trustworthy strangers or do they need to earn your trust?",
                "People must earn trust",
                "People are trustworthy by default",
                new List<string> { "human-nature", "trust" }
            ),
            (
                "How confident are you that science and empirical evidence are the best tools for understanding reality?",
                "Other ways of knowing are equally valid",
                "Science is the most reliable path to truth",
                new List<string> { "epistemology", "science-trust" }
            ),
            (
                "How much do you think a person's life outcomes are shaped by their own choices versus circumstances beyond their control?",
                "Almost entirely circumstances",
                "Almost entirely personal choices",
                new List<string> { "responsibility", "human-nature", "systemic-thinking" }
            ),
            (
                "How strongly do you feel a sense of duty to your own nation versus to all of humanity?",
                "Duty is to all humanity equally",
                "My nation comes first",
                new List<string> { "nationalism", "group-loyalty" }
            ),
            (
                "How much do you think economic inequality is a serious moral problem that society must actively address?",
                "Inequality is natural and acceptable",
                "Inequality is a serious injustice requiring policy action",
                new List<string> { "political-economic", "fairness", "equality" }
            ),
            (
                "How important is it to you personally to live by a coherent set of moral or religious principles?",
                "Principles are too rigid; I navigate case by case",
                "Living by firm principles is essential to who I am",
                new List<string> { "moral-foundations", "religion", "values" }
            ),
            (
                "When a law is unjust, how acceptable is it to break that law in protest?",
                "Breaking the law is never justified",
                "Civil disobedience is sometimes a moral duty",
                new List<string> { "rule-following", "civic-duty", "moral-reasoning" }
            ),
            (
                "How much do governments have a right to restrict individual freedoms in the name of public safety or security?",
                "Rights are absolute — government has no such right",
                "Security can justify significant limits on freedom",
                new List<string> { "authority", "security", "autonomy" }
            ),
            (
                "How optimistic are you about humanity's ability to solve major global problems like climate change and poverty?",
                "Very pessimistic — these problems are beyond us",
                "Very optimistic — human ingenuity will prevail",
                new List<string> { "human-nature", "existential", "environment" }
            ),
            (
                "How much weight do you give to the needs and rights of future generations when making decisions today?",
                "Present needs must come first",
                "Future generations' wellbeing carries equal weight",
                new List<string> { "intergenerational-justice", "moral-calculus" }
            ),
            (
                "How important is it that public life and government policy are kept completely separate from religious belief?",
                "Religion should inform public life and policy",
                "Strict separation of religion and state is essential",
                new List<string> { "religion", "authority", "political-social" }
            ),
            (
                "How much do you value stability and predictability versus novelty and risk in your own life?",
                "I strongly prefer stability",
                "I actively seek novelty and risk",
                new List<string> { "risk-tolerance", "change-orientation", "values" }
            ),
            (
                "How much do the interests of people outside your country matter to you in your moral decision-making?",
                "My community and country come first",
                "All people deserve equal moral consideration",
                new List<string> { "compassion", "group-loyalty", "nationalism" }
            ),
        };

        // Filter out already-asked questions
        var unseen = ExcludeAsked(pool,
            profile.AskedQuestionHashes,
            q => q.Question,
            q => null,
            _ => null);

        // Prefer an entry targeting the requested dimension, otherwise fall back
        var preferred = preferDimension != null
            ? unseen.Where(q => q.Dimensions.Contains(preferDimension)).ToList()
            : new List<(string Question, string MinLabel, string MaxLabel, List<string> Dimensions)>();

        var selected = preferred.Any()
            ? preferred[new Random().Next(preferred.Count)]
            : unseen[new Random().Next(unseen.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.ScaleQuestion,
            Content = new InteractionContent
            {
                Question = selected.Question,
                Format = InteractionFormat.Scale,
                MinValue = 1,
                MaxValue = 10,
                MinLabel = selected.MinLabel,
                MaxLabel = selected.MaxLabel
            },
            TargetedDimensions = selected.Dimensions
        };
    }

    // Keep overload for any callers that still pass explicit dimension + labels
    public UserInteraction GenerateScaleQuestion(
        UserProfile profile,
        string dimension,
        string lowLabel,
        string highLabel)
        => GenerateScaleQuestion(profile, preferDimension: dimension);

    /// <summary>
    /// Generate value ranking exercise
    /// </summary>
    public UserInteraction GenerateValueRankingQuestion(UserProfile profile)
    {
        var valueSets = new List<(string Question, List<string> Values, List<string> Dimensions)>
        {
            (
                "Rank these values in order of personal importance (1 = most important):",
                new List<string> { "Freedom", "Security", "Equality", "Prosperity", "Community" },
                new List<string> { "values", "political-economic" }
            ),
            (
                "Rank these moral virtues by how much you personally admire them:",
                new List<string> { "Justice", "Mercy", "Truth", "Loyalty", "Compassion" },
                new List<string> { "moral-foundations", "values" }
            ),
            (
                "Rank these character traits by how important they are in a good person:",
                new List<string> { "Wisdom", "Courage", "Temperance", "Authenticity", "Humility" },
                new List<string> { "values", "moral-foundations" }
            ),
            (
                "Rank these societal priorities from most to least important:",
                new List<string> { "Individual Rights", "Social Harmony", "Environmental Protection", "Economic Growth", "Cultural Preservation" },
                new List<string> { "political-social", "political-economic", "environment" }
            ),
            (
                "Rank what you think matters most in a just society:",
                new List<string> { "Equal opportunity", "Equal outcomes", "Meritocracy", "Social safety net", "Rule of law" },
                new List<string> { "fairness", "political-economic", "justice" }
            ),
            (
                "Rank these sources of meaning in life from most to least meaningful to you:",
                new List<string> { "Relationships", "Contribution to society", "Personal achievement", "Spiritual connection", "Experiences and pleasure" },
                new List<string> { "life-purpose", "values", "spirituality" }
            ),
            (
                "Rank the responsibilities you feel most strongly, from most to least obligatory:",
                new List<string> { "To your family", "To your country", "To humanity", "To future generations", "To yourself" },
                new List<string> { "moral-foundations", "group-loyalty", "individualism-collectivism" }
            ),
            (
                "Rank what you think are the most important qualities of a democracy:",
                new List<string> { "Free elections", "Protection of minority rights", "Free press", "Accountability of leaders", "Civic participation" },
                new List<string> { "democracy", "authority", "rights" }
            ),
            (
                "Rank these when thinking about what makes an economy fair:",
                new List<string> { "Rewarding hard work and talent", "Providing a basic floor for everyone", "Preventing extreme inequality", "Maximizing total wealth", "Ensuring equal starting conditions" },
                new List<string> { "political-economic", "fairness", "equality" }
            ),
            (
                "Rank what matters most to you when choosing where to live:",
                new List<string> { "Safety", "Job opportunities", "Cultural diversity", "Natural environment", "Strong community ties" },
                new List<string> { "values", "community", "environment" }
            ),
            (
                "Rank these approaches to dealing with crime:",
                new List<string> { "Prevention through social support", "Rehabilitation of offenders", "Strict deterrence", "Victim restitution", "Incapacitation for public safety" },
                new List<string> { "justice", "punishment", "compassion" }
            ),
            (
                "Rank what you believe should drive scientific research priorities:",
                new List<string> { "Curing disease", "Understanding the universe", "Addressing climate change", "Economic competitiveness", "Military advantage" },
                new List<string> { "science-trust", "values", "environment" }
            ),
            (
                "Rank what you think education should most emphasize:",
                new List<string> { "Critical thinking", "Vocational skills", "Cultural literacy", "STEM subjects", "Emotional intelligence and character" },
                new List<string> { "education", "values", "political-social" }
            ),
            (
                "Rank these in terms of whose welfare you feel most responsibility for:",
                new List<string> { "Your immediate family", "Your local community", "Your fellow citizens", "All humans alive today", "Future generations" },
                new List<string> { "group-loyalty", "moral-foundations", "intergenerational-justice" }
            ),
            (
                "Rank these aspects of environmental policy from highest to lowest priority:",
                new List<string> { "Reducing carbon emissions", "Protecting biodiversity", "Clean water and air", "Transitioning workers in fossil fuel industries", "Developing green technology" },
                new List<string> { "environment", "political-economic" }
            ),
        };

        // Filter out already-asked rankings
        var pool = ExcludeAsked(valueSets,
            profile.AskedQuestionHashes,
            v => v.Question,
            v => v.Values,
            _ => null);

        var unexploredPool = pool
            .Where(v => !v.Dimensions.All(d => profile.ExploredDimensions.Contains(d)))
            .ToList();
        if (!unexploredPool.Any()) unexploredPool = pool;

        var selectedSet = unexploredPool[new Random().Next(unexploredPool.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.ValueRanking,
            Content = new InteractionContent
            {
                Question = selectedSet.Question,
                Format = InteractionFormat.Ranking,
                Options = selectedSet.Values
            },
            TargetedDimensions = selectedSet.Dimensions
        };
    }

    /// <summary>
    /// Generate initial survey multiple choice question.
    /// Pass <paramref name="forcedIndex"/> to override the profile interaction count
    /// when pre-generating multiple questions in a loop (otherwise they all resolve to the same index).
    /// Uses random selection from unseen questions to avoid deterministic ordering.
    /// </summary>
    public UserInteraction GenerateInitialSurveyQuestion(UserProfile profile, int? forcedIndex = null)
    {
        var questions = GetInitialSurveyQuestionPool();

        var pool = ExcludeAsked(questions,
            profile.AskedQuestionHashes,
            q => q.Question,
            q => q.Options,
            _ => null);

        // Random selection from unseen pool for variety
        var selectedQuestion = pool[new Random().Next(pool.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.StatementAgreement,
            Content = new InteractionContent
            {
                Question = selectedQuestion.Question,
                Format = InteractionFormat.MultipleChoice,
                Options = selectedQuestion.Options
            },
            TargetedDimensions = selectedQuestion.Dimensions
        };
    }

    /// <summary>
    /// Select an initial survey question that targets specific high-uncertainty dimensions.
    /// Returns null if no matching question is found.
    /// </summary>
    public UserInteraction? GenerateInitialSurveyQuestionTargeting(
        UserProfile profile, 
        List<string> targetDimensions)
    {
        // Reuse the same question pool as GenerateInitialSurveyQuestion
        var questions = GetInitialSurveyQuestionPool();
        
        // Score each question by how many target dimensions it covers
        var scored = questions
            .Select(q => new
            {
                Question = q,
                Score = q.Dimensions.Count(d => targetDimensions.Any(td =>
                    d.Contains(td, StringComparison.OrdinalIgnoreCase) ||
                    td.Contains(d, StringComparison.OrdinalIgnoreCase)))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (!scored.Any())
        {
            _logger.LogInformation("No targeted initial survey question found for dimensions: {Dimensions}",
                string.Join(", ", targetDimensions));
            return null;
        }

        // Pick the best-matching question that hasn't been asked yet
        foreach (var candidate in scored)
        {
            var hash = ComputeQuestionHash(candidate.Question.Question, candidate.Question.Options);
            if (!profile.AskedQuestionHashes.Contains(hash))
            {
                profile.AskedQuestionHashes.Add(hash);
                _logger.LogInformation(
                    "Selected targeted initial question for dimensions {TargetDims}: {Question} (score: {Score})",
                    string.Join(", ", targetDimensions), candidate.Question.Question, candidate.Score);

                return new UserInteraction
                {
                    UserId = profile.Id,
                    Type = InteractionType.StatementAgreement,
                    Content = new InteractionContent
                    {
                        Question = candidate.Question.Question,
                        Format = InteractionFormat.MultipleChoice,
                        Options = candidate.Question.Options
                    },
                    TargetedDimensions = candidate.Question.Dimensions
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the pool of initial survey questions. Extracted to a method
    /// so it can be shared between GenerateInitialSurveyQuestion and
    /// GenerateInitialSurveyQuestionTargeting.
    /// </summary>
    private List<(string Question, List<string> Options, List<string> Dimensions)> GetInitialSurveyQuestionPool()
    {
        return new List<(string Question, List<string> Options, List<string> Dimensions)>
        {
            (
                "What do you value most in life?",
                new List<string>
                {
                    "Personal freedom and independence",
                    "Strong relationships and community",
                    "Achievement and success",
                    "Knowledge and understanding",
                    "Helping others and making a difference"
                },
                new List<string> { "values", "individualism-collectivism" }
            ),
            (
                "When making an important decision, what matters most to you?",
                new List<string>
                {
                    "What will produce the best outcome for everyone involved",
                    "Whether it aligns with my core principles",
                    "What feels right in my gut",
                    "What authority figures or tradition suggest",
                    "What benefits me and those closest to me"
                },
                new List<string> { "moral-reasoning", "consequentialism-deontology" }
            ),
            (
                "How do you view human nature?",
                new List<string>
                {
                    "People are fundamentally good and trustworthy",
                    "People are naturally selfish but can be taught cooperation",
                    "People are shaped by their environment and circumstances",
                    "Human nature is complex and varies greatly by individual",
                    "People need structure and rules to behave well"
                },
                new List<string> { "human-nature", "trust" }
            ),
            (
                "What role should government play in society?",
                new List<string>
                {
                    "Minimal - protect basic rights and stay out of the way",
                    "Active - ensure fairness and support those in need",
                    "Balanced - provide infrastructure but respect freedom",
                    "Strong - maintain order and preserve tradition",
                    "Democratic - reflect the will of the majority"
                },
                new List<string> { "political-economic", "authority" }
            ),
            (
                "When you see someone in need, what's your first instinct?",
                new List<string>
                {
                    "Help immediately without hesitation",
                    "Assess whether they deserve help",
                    "Consider whether helping would create dependency",
                    "Feel sympathy but look to institutions to help",
                    "Help if I have the resources and it won't harm me"
                },
                new List<string> { "compassion", "moral-foundations" }
            ),
            (
                "How do you think about your own identity?",
                new List<string>
                {
                    "I'm primarily an individual with my own unique path",
                    "I'm defined by the groups and communities I belong to",
                    "I'm shaped by my culture and upbringing, for better or worse",
                    "My identity is fluid and evolves over time",
                    "I'm part of something larger - humanity, nature, or the cosmos"
                },
                new List<string> { "identity", "individualism-collectivism" }
            ),
            (
                "How do you generally form your beliefs?",
                new List<string>
                {
                    "By following the evidence wherever it leads",
                    "Based on what my community and trusted people believe",
                    "Through personal experience and intuition",
                    "By reasoning carefully from first principles",
                    "From religious or philosophical texts and traditions"
                },
                new List<string> { "epistemology", "authority" }
            ),
            (
                "What is the most important quality in a leader?",
                new List<string>
                {
                    "Competence and results",
                    "Integrity and honesty",
                    "Empathy and care for people",
                    "Decisiveness and strength",
                    "Vision and inspiration"
                },
                new List<string> { "leadership", "authority", "values" }
            ),
            (
                "How do you feel about social and cultural change?",
                new List<string>
                {
                    "Progress is generally good and we should embrace it",
                    "Change should be gradual and carefully evaluated",
                    "Proven traditions and institutions should be preserved",
                    "We need radical change to fix serious injustices",
                    "I'm skeptical of both extreme conservatism and radical change"
                },
                new List<string> { "political-social", "tradition", "change-orientation" }
            ),
            (
                "What matters most when evaluating whether an action is right or wrong?",
                new List<string>
                {
                    "The consequences and how many people benefit or are harmed",
                    "Whether it follows universal moral rules",
                    "Whether virtuous people of good character would do it",
                    "Whether it respects everyone's rights and autonomy",
                    "Whether it aligns with religious or cultural norms"
                },
                new List<string> { "moral-reasoning", "moral-foundations", "values" }
            )
        };
    }

    /// <summary>
    /// Compute a hash for a question to detect duplicates
    /// </summary>
    private string ComputeQuestionHash(string question, List<string>? options = null)
    {
        var content = question ?? "";
        if (options?.Any() == true)
        {
            content += "|" + string.Join("|", options);
        }
        return content.GetHashCode().ToString();
    }

    /// <summary>
    /// Generate a general multiple choice question based on uncertain areas
    /// </summary>
    public UserInteraction GenerateMultipleChoiceQuestion(UserProfile profile, BeliefSnapshot snapshot)
    {
        var questions = new List<(string Question, List<string> Options, List<string> Dimensions)>
        {
            (
                "Which statement best describes your view on wealth inequality?",
                new List<string>
                {
                    "Inequality is natural and motivates people to work harder",
                    "Some inequality is okay, but extremes should be reduced",
                    "Society should actively work toward economic equality",
                    "Inequality is acceptable as long as everyone has basic needs met",
                    "The focus should be on equal opportunity, not equal outcomes"
                },
                new List<string> { "political-economic", "fairness" }
            ),
            (
                "How do you feel about breaking rules for a good cause?",
                new List<string>
                {
                    "Rules exist for a reason and should always be followed",
                    "Sometimes breaking rules is justified if the cause is important enough",
                    "The outcome matters more than following rules",
                    "It depends entirely on the specific situation",
                    "I'd feel uncomfortable breaking rules even for good reasons"
                },
                new List<string> { "moral-reasoning", "rule-following" }
            ),
            (
                "What's your view on tradition and cultural heritage?",
                new List<string>
                {
                    "Traditions should be preserved and respected",
                    "Keep good traditions, discard harmful ones",
                    "Society should constantly evolve beyond old traditions",
                    "Traditions are interesting but shouldn't dictate modern life",
                    "Each generation should create its own values"
                },
                new List<string> { "political-social", "tradition" }
            ),
            (
                "How much do you trust your intuition versus logical analysis?",
                new List<string>
                {
                    "I trust my gut feelings - they're usually right",
                    "I prefer to think things through logically",
                    "I balance both intuition and analysis",
                    "It depends on the situation",
                    "I trust data and facts over feelings"
                },
                new List<string> { "epistemology", "decision-making" }
            ),
            (
                "What motivates you to be a good person?",
                new List<string>
                {
                    "It's the right thing to do regardless of consequences",
                    "I want to make the world a better place",
                    "It makes me feel good about myself",
                    "It's what my community/faith expects",
                    "I'd want others to treat me the same way"
                },
                new List<string> { "moral-foundations", "motivation" }
            ),
            (
                "How do you view experts and authority figures?",
                new List<string>
                {
                    "They deserve respect and should generally be followed",
                    "I respect expertise but question authority",
                    "Everyone's opinion is equally valid",
                    "Authority should always be questioned",
                    "I defer to experts in their fields"
                },
                new List<string> { "authority", "epistemology" }
            ),
            (
                "What's most important for a good life?",
                new List<string>
                {
                    "Personal happiness and fulfillment",
                    "Contributing to something larger than myself",
                    "Strong relationships with family and friends",
                    "Success and achievement",
                    "Living according to my principles"
                },
                new List<string> { "values", "life-purpose" }
            ),
            (
                "How do you view change in society?",
                new List<string>
                {
                    "Change is exciting and usually positive",
                    "Change is necessary but should happen gradually",
                    "Too much change is destabilizing",
                    "Change should only happen when absolutely necessary",
                    "We need radical change to fix current problems"
                },
                new List<string> { "political-social", "change-orientation" }
            ),
            // ── Environmental & Climate ──────────────────────────────────────────────
            (
                "How seriously do you take the threat of climate change?",
                new List<string>
                {
                    "It's the defining crisis of our time requiring immediate action",
                    "It's real and serious, but solutions must balance economic costs",
                    "It's overhyped; natural cycles explain most of the change",
                    "I'm uncertain about the extent of human impact",
                    "It's a global problem that no single country can solve alone"
                },
                new List<string> { "environment", "science-trust", "political-economic" }
            ),
            (
                "Who bears the greatest responsibility for environmental problems?",
                new List<string>
                {
                    "Corporations and industries - individuals can't make a difference",
                    "Consumers - demand drives pollution",
                    "Governments that fail to regulate effectively",
                    "All of us equally - it's a collective failure",
                    "Wealthy nations that industrialized first"
                },
                new List<string> { "environment", "responsibility", "political-economic" }
            ),
            // ── Technology & AI ──────────────────────────────────────────────────────
            (
                "How do you feel about artificial intelligence taking over more human jobs?",
                new List<string>
                {
                    "Exciting - it frees us for more creative and meaningful work",
                    "Concerning - we need strong retraining and social support systems",
                    "Alarming - it threatens human dignity and purpose",
                    "Inevitable - we should prepare pragmatically",
                    "Good for efficiency, bad for the workers affected"
                },
                new List<string> { "technology", "economic-change", "human-dignity" }
            ),
            (
                "Should individuals have absolute privacy from surveillance?",
                new List<string>
                {
                    "Yes - privacy is a fundamental right, no exceptions",
                    "Some surveillance is acceptable for serious security threats",
                    "I'd trade privacy for safety if it's proportionate",
                    "Corporations invade privacy more than governments do",
                    "Privacy and security need to be carefully balanced case by case"
                },
                new List<string> { "privacy", "authority", "security" }
            ),
            (
                "How should social media platforms handle harmful but legal speech?",
                new List<string>
                {
                    "Remove it - platforms have a responsibility to prevent harm",
                    "Label it, but don't remove it",
                    "Leave it entirely - free speech must be protected",
                    "Let communities moderate themselves",
                    "Government should set clear rules rather than leaving it to companies"
                },
                new List<string> { "free-speech", "censorship", "authority" }
            ),
            // ── Justice & Crime ──────────────────────────────────────────────────────
            (
                "What is the primary purpose of prison?",
                new List<string>
                {
                    "Rehabilitation - helping offenders become productive citizens",
                    "Deterrence - making crime too costly to risk",
                    "Punishment - justice demands proportional consequences",
                    "Public safety - keeping dangerous people off the streets",
                    "Restoration - repairing harm to victims and communities"
                },
                new List<string> { "justice", "punishment", "compassion" }
            ),
            (
                "How do you view the relationship between criminal behavior and personal responsibility?",
                new List<string>
                {
                    "People always have a choice - circumstances don't excuse crime",
                    "Poverty and systemic disadvantage explain most crime",
                    "Both individual choices and social conditions matter equally",
                    "Mental health and trauma are more relevant than we admit",
                    "The law should focus on consequences, not causes"
                },
                new List<string> { "justice", "human-nature", "systemic-thinking" }
            ),
            // ── Equality & Social Justice ────────────────────────────────────────────
            (
                "How do you view systemic racism and structural inequality?",
                new List<string>
                {
                    "It's a major ongoing problem that requires active policy solutions",
                    "Discrimination exists but solutions should be race-neutral",
                    "Historical inequities matter, but individual effort determines outcomes today",
                    "Focusing on race perpetuates division rather than healing it",
                    "Intersecting systems of inequality affect many groups, not just race"
                },
                new List<string> { "social-justice", "equality", "systemic-thinking" }
            ),
            (
                "What's your view on affirmative action in university admissions?",
                new List<string>
                {
                    "Necessary to correct historical injustice and increase diversity",
                    "Wrong - all applicants should be judged solely on merit",
                    "Better to focus on socioeconomic disadvantage than race",
                    "Diversity benefits everyone, so some preference is justified",
                    "Race-conscious policies create new forms of unfairness"
                },
                new List<string> { "equality", "fairness", "social-justice" }
            ),
            // ── Healthcare & Welfare ─────────────────────────────────────────────────
            (
                "Is healthcare a right or a service to be purchased?",
                new List<string>
                {
                    "A fundamental right - society must provide it for everyone",
                    "A service - but basic coverage should be universally available",
                    "Primarily a private service with a safety net for the very poor",
                    "A right, but one that requires personal responsibility in using it",
                    "Not a right - but markets will deliver better care than government"
                },
                new List<string> { "healthcare", "political-economic", "rights" }
            ),
            (
                "How do you feel about a universal basic income (UBI)?",
                new List<string>
                {
                    "It's a great idea that would reduce poverty and anxiety",
                    "Interesting but it would reduce work incentives",
                    "Too expensive and it treats symptoms rather than causes",
                    "Worth piloting carefully before any large rollout",
                    "It undermines the dignity that comes from earning a living"
                },
                new List<string> { "political-economic", "human-dignity", "welfare" }
            ),
            // ── Religion & Spirituality ──────────────────────────────────────────────
            (
                "What role should religion play in public life and policy?",
                new List<string>
                {
                    "None - government must be strictly secular",
                    "Religious communities should influence policy like any other group",
                    "Religious values are a valid moral foundation for law",
                    "Religion should stay private; public life should be guided by reason",
                    "We should draw on the wisdom of many religious traditions"
                },
                new List<string> { "religion", "authority", "political-social" }
            ),
            (
                "How do you reconcile science and religious or spiritual belief?",
                new List<string>
                {
                    "Science and religion address different questions - no conflict",
                    "Where they conflict, science wins",
                    "Where they conflict, faith wins",
                    "Both are incomplete - the truth lies somewhere between them",
                    "I don't hold religious beliefs; science is my framework"
                },
                new List<string> { "religion", "science-trust", "epistemology" }
            ),
            // ── International & Nation-State ─────────────────────────────────────────
            (
                "Should wealthy countries take significantly more refugees and migrants?",
                new List<string>
                {
                    "Yes - it's a moral obligation given global inequality",
                    "Yes, but with careful vetting and integration support",
                    "No - nations have a right and duty to control their borders",
                    "It depends on whether migrants can integrate successfully",
                    "The focus should be on improving conditions in origin countries"
                },
                new List<string> { "immigration", "compassion", "group-loyalty" }
            ),
            (
                "What's your view on national sovereignty versus global cooperation?",
                new List<string>
                {
                    "Global problems need global solutions - nations must cede some sovereignty",
                    "Nations have a right to make their own decisions regardless of global norms",
                    "International institutions are important but often overreach",
                    "We need deeper global cooperation but better democratic accountability",
                    "A balance of independent nations is healthier than global governance"
                },
                new List<string> { "nationalism", "global-cooperation", "authority" }
            ),
            // ── Education & Knowledge ────────────────────────────────────────────────
            (
                "What is the primary purpose of education?",
                new List<string>
                {
                    "Transmitting a shared cultural heritage and civic values",
                    "Developing critical thinking and the ability to question everything",
                    "Preparing people for productive careers in the economy",
                    "Fostering personal growth and self-discovery",
                    "Reducing inequality by giving everyone a fair start"
                },
                new List<string> { "education", "values", "political-social" }
            ),
            (
                "How do you feel about meritocracy?",
                new List<string>
                {
                    "It's broadly fair - hard work and talent should be rewarded",
                    "It's a useful ideal but deeply undermined by privilege and luck",
                    "It ignores structural barriers that not everyone can overcome",
                    "Partly fair, but emphasizes the wrong things (e.g. credentials over wisdom)",
                    "It's a myth that legitimizes inequality by blaming the poor"
                },
                new List<string> { "fairness", "equality", "political-economic" }
            ),
            // ── Death, End-of-Life & Existential ────────────────────────────────────
            (
                "How do you feel about physician-assisted dying for terminally ill patients?",
                new List<string>
                {
                    "It should be a personal right available to all competent adults",
                    "Acceptable in extreme suffering, within strict safeguards",
                    "Wrong - medicine should relieve pain, not end life",
                    "Better palliative care is the answer, not assisted dying",
                    "It should be an individual right only where all other options are exhausted"
                },
                new List<string> { "autonomy", "sanctity-of-life", "compassion" }
            ),
            (
                "What happens after we die?",
                new List<string>
                {
                    "Nothing - consciousness simply stops",
                    "Our souls continue in some form of afterlife",
                    "Our energy or consciousness merges back with the universe",
                    "Open question - I don't think anyone really knows",
                    "Our legacy and impact on others is our 'afterlife'"
                },
                new List<string> { "spirituality", "religion", "existential" }
            ),
            // ── Animal Rights & Ethics ───────────────────────────────────────────────
            (
                "How should we weigh animal welfare against human interests?",
                new List<string>
                {
                    "Animals have rights similar to humans and must be protected accordingly",
                    "Animal suffering matters morally, but human needs come first",
                    "Animals are resources to be used humanely",
                    "We have a duty of stewardship over animals, not ownership",
                    "Wild animals are part of nature; only suffering of pets matters"
                },
                new List<string> { "animal-rights", "compassion", "values" }
            ),
            // ── Work, Meaning & Economy ──────────────────────────────────────────────
            (
                "What is the relationship between work and personal identity?",
                new List<string>
                {
                    "Work is central to who I am and gives me purpose",
                    "Work is important but just one part of a fuller life",
                    "Work is primarily a means to an end",
                    "We over-identify with jobs in a way that's harmful",
                    "Meaningful work is a privilege; most people just need income"
                },
                new List<string> { "values", "human-dignity", "life-purpose" }
            ),
            (
                "How do you think about personal responsibility versus systemic factors in life outcomes?",
                new List<string>
                {
                    "Personal choices and character are the primary determinants",
                    "Systemic and structural factors are the primary determinants",
                    "Both matter deeply and interact in complex ways",
                    "It varies hugely depending on someone's circumstances",
                    "Emphasizing either alone makes us blind to an important truth"
                },
                new List<string> { "systemic-thinking", "responsibility", "human-nature" }
            ),
        };

        // Filter out already-asked questions first, then apply dimension logic
        var pool = ExcludeAsked(questions,
            profile.AskedQuestionHashes,
            q => q.Question,
            q => q.Options,
            _ => null);

        var unexploredPool = pool
            .Where(q => !q.Dimensions.All(d => profile.ExploredDimensions.Contains(d)))
            .ToList();
        if (!unexploredPool.Any()) unexploredPool = pool;

        var uncertainAreas = snapshot.Statistics.UncertainAreas;
        var prioritizedPool = unexploredPool
            .Where(q => q.Dimensions.Any(d => uncertainAreas.Contains(d)))
            .ToList();

        var selectedQuestion = prioritizedPool.Any()
            ? prioritizedPool[new Random().Next(prioritizedPool.Count)]
            : unexploredPool[new Random().Next(unexploredPool.Count)];

        foreach (var dim in selectedQuestion.Dimensions)
        {
            var existingDim = snapshot.Dimensions.FirstOrDefault(d => d.Name == dim);
            if (existingDim?.Confidence > 0.7)
                profile.ExploredDimensions.Add(dim);
        }

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.StatementAgreement,
            Content = new InteractionContent
            {
                Question = selectedQuestion.Question,
                Format = InteractionFormat.MultipleChoice,
                Options = selectedQuestion.Options
            },
            TargetedDimensions = selectedQuestion.Dimensions
        };
    }

    /// <summary>
    /// Generate a moral dilemma as multiple choice
    /// </summary>
    public UserInteraction GenerateMoralDilemmaMultipleChoice(UserProfile profile, BeliefSnapshot snapshot)
    {
        var dilemmas = new List<(string Scenario, string Question, List<string> Options, List<string> Dimensions)>
        {
            (
                "You find a wallet with $500 and the owner's ID. You're struggling financially this month.",
                "What would you do?",
                new List<string>
                {
                    "Return it immediately with all the money",
                    "Return it but keep some money as a 'finder's fee'",
                    "Keep the money - they probably won't miss it",
                    "Turn it in to the police",
                    "Try to return it directly, but keep it if that's too difficult"
                },
                new List<string> { "honesty", "consequentialism" }
            ),
            (
                "A close friend confides they've been having an affair. Their spouse, who you also know, seems unaware.",
                "How do you respond?",
                new List<string>
                {
                    "Tell the spouse - they deserve to know",
                    "Stay out of it completely",
                    "Urge my friend to come clean but don't intervene",
                    "Support my friend but express disapproval",
                    "It's not my place to judge their choices"
                },
                new List<string> { "loyalty", "honesty", "moral-judgment" }
            ),
            (
                "You witness your employer doing something unethical but not illegal. Speaking up could cost you your job.",
                "What do you do?",
                new List<string>
                {
                    "Report it regardless of consequences",
                    "Report it anonymously if possible",
                    "Keep quiet - I need this job",
                    "Confront my employer privately first",
                    "Document it but wait for the right moment"
                },
                new List<string> { "integrity", "self-interest", "courage" }
            ),
            (
                "A self-driving car must choose: swerve and kill one pedestrian, or continue and kill five passengers.",
                "What should it be programmed to do?",
                new List<string>
                {
                    "Save the most lives (kill the one pedestrian)",
                    "Protect its passengers (kill the five pedestrians)",
                    "Do nothing and let chance decide",
                    "There's no right answer to this",
                    "The car should protect those not responsible for the situation"
                },
                new List<string> { "consequentialism", "moral-calculus", "utilitarianism" }
            ),
            (
                "Your country is considering accepting refugees, but polls show most citizens are opposed.",
                "What should the government do?",
                new List<string>
                {
                    "Accept refugees - it's the moral thing to do",
                    "Follow the will of the people",
                    "Accept some but with strict limits",
                    "Prioritize citizens' concerns over refugees",
                    "Only accept refugees if it benefits the country"
                },
                new List<string> { "compassion", "democracy", "group-loyalty" }
            ),
            (
                "You discover a way to cheat on your taxes that's unlikely to be detected. It would save you thousands.",
                "What do you do?",
                new List<string>
                {
                    "Never - I pay what I owe even if I disagree with how it's spent",
                    "Depends on what the government is doing with tax money",
                    "I'd take the deduction - the system is broken anyway",
                    "Everyone does it, so it's effectively expected",
                    "I'd feel too guilty to do it"
                },
                new List<string> { "rule-following", "civic-duty", "self-interest" }
            ),
            (
                "A pharmaceutical company develops a life-saving drug but prices it so high that most who need it can't afford it.",
                "How do you view this?",
                new List<string>
                {
                    "They have a right to charge what the market will bear",
                    "Morally wrong - profit shouldn't override human life",
                    "Government should regulate pricing of essential medicines",
                    "Acceptable as long as some charity access is provided",
                    "The company should recoup costs but not maximize profit"
                },
                new List<string> { "capitalism", "healthcare", "compassion" }
            ),
            (
                "Your teenage child wants to drop out of school to pursue an unlikely career in the arts. They're passionate but you doubt it will work.",
                "What do you do?",
                new List<string>
                {
                    "Support their passion - happiness matters more than security",
                    "Insist they finish school first as a backup plan",
                    "Absolutely forbid it - they're too young to decide",
                    "Encourage them but explain the risks honestly",
                    "Let them try but require a realistic timeline/plan"
                },
                new List<string> { "parenting", "pragmatism", "autonomy" }
            ),
            (
                "A stranger online is being harassed and asks for your help. Getting involved might expose you to backlash.",
                "What do you do?",
                new List<string>
                {
                    "Help immediately - it's the right thing to do",
                    "Help anonymously to avoid personal risk",
                    "Stay out of it - not my problem",
                    "Report the harassment to the platform",
                    "Offer support privately but don't get publicly involved"
                },
                new List<string> { "courage", "solidarity", "risk-tolerance" }
            ),
            // ── Whistleblowing & Institutional Trust ────────────────────────────────
            (
                "A government whistleblower leaks classified information revealing illegal surveillance of citizens. Lives may be at risk from the disclosure.",
                "How do you view the whistleblower?",
                new List<string>
                {
                    "A hero - the public's right to know outweighs the risk",
                    "Misguided - working within the system is the right path",
                    "A traitor who endangered innocent people",
                    "Depends on the specific content and harm caused",
                    "A public servant who broke the law but exposed real wrongdoing"
                },
                new List<string> { "civic-duty", "authority", "transparency" }
            ),
            (
                "A journalist obtains documents proving a popular politician is corrupt, but publishing them would use illegally obtained information.",
                "Should they publish?",
                new List<string>
                {
                    "Yes - the public interest justifies it",
                    "No - the means matter as much as the end",
                    "Only after confirming the content is authentic",
                    "Depends on the severity of the corruption",
                    "Yes, but they should also expose how they obtained it"
                },
                new List<string> { "transparency", "rule-following", "consequentialism" }
            ),
            // ── Medical Ethics ───────────────────────────────────────────────────────
            (
                "A hospital has one ventilator and two patients who need it to survive. One is elderly with weeks to live, the other is young with a full life ahead.",
                "How should the decision be made?",
                new List<string>
                {
                    "Give it to the young person - they have more life ahead",
                    "First come, first served - who arrived first gets it",
                    "Random lottery - all lives are equally valuable",
                    "Give it to whoever is more likely to survive long-term",
                    "A medical ethics committee should decide case by case"
                },
                new List<string> { "moral-calculus", "justice", "sanctity-of-life" }
            ),
            (
                "Genetic testing reveals your sibling has a hereditary disease you might also carry. They've asked you not to tell anyone.",
                "If your partner wants children, what do you do?",
                new List<string>
                {
                    "Get tested myself and tell my partner regardless of my sibling's wishes",
                    "Respect my sibling's request and keep the information private",
                    "Encourage my sibling to reconsider, but don't act without their permission",
                    "Get tested silently and tell my partner only if I'm also a carrier",
                    "Consult a genetic counselor for guidance before deciding"
                },
                new List<string> { "loyalty", "autonomy", "honesty", "family" }
            ),
            // ── Environmental Trade-offs ─────────────────────────────────────────────
            (
                "A community can have a factory that provides 500 jobs but will pollute a local river used by downstream communities.",
                "What should happen?",
                new List<string>
                {
                    "The economic needs of the local community come first",
                    "Environmental harm is unacceptable regardless of economic benefits",
                    "Allow it with strict pollution limits and compensation for downstream communities",
                    "Let the communities involve decide democratically",
                    "Reject it and invest in finding a cleaner alternative"
                },
                new List<string> { "environment", "economic-change", "community" }
            ),
            // ── Digital & Privacy ────────────────────────────────────────────────────
            (
                "Your employer wants to monitor all employee communications to detect insider threats. It would also reveal personal conversations.",
                "Is this acceptable?",
                new List<string>
                {
                    "No - privacy at work is a right, even on company hardware",
                    "Yes - the employer owns the systems and has security obligations",
                    "Only if employees are clearly informed and consent",
                    "Only for roles with access to highly sensitive information",
                    "Acceptable only if personal communications are excluded from review"
                },
                new List<string> { "privacy", "authority", "security" }
            ),
            // ── Intergenerational Justice ────────────────────────────────────────────
            (
                "Current generations are benefiting from borrowing money (or environmental credit) that future generations will have to repay.",
                "How do you feel about this?",
                new List<string>
                {
                    "Deeply wrong - we have obligations to future people who can't vote",
                    "It's always been this way; future generations will be richer anyway",
                    "We should invest in things that genuinely grow future wellbeing",
                    "Hard to balance, but present needs often genuinely outweigh future costs",
                    "Future generations will have better tools to deal with these problems"
                },
                new List<string> { "intergenerational-justice", "environment", "moral-calculus" }
            ),
            // ── Cultural Conflict ────────────────────────────────────────────────────
            (
                "An immigrant community practices a cultural tradition that is legal but that many in the host country find deeply offensive.",
                "What's the right response?",
                new List<string>
                {
                    "Tolerance - cultural practices should be respected if legal",
                    "Integration requires adapting to the host culture's norms over time",
                    "The host culture has no right to impose its values on minorities",
                    "Legal doesn't mean acceptable - communities can express disapproval",
                    "Open dialogue and mutual understanding is the only real path"
                },
                new List<string> { "cultural-pluralism", "tradition", "group-loyalty" }
            ),
            // ── Truth & Kindness ─────────────────────────────────────────────────────
            (
                "Your friend shows you their new business plan. You can see it will likely fail but they're very excited about it.",
                "What do you say?",
                new List<string>
                {
                    "Be completely honest - they need to hear it to avoid wasting time and money",
                    "Support them enthusiastically - they'll learn from the experience",
                    "Share concerns gently but ultimately respect their decision",
                    "Ask questions that help them think it through rather than telling them directly",
                    "Only mention concerns if they ask for honest feedback"
                },
                new List<string> { "honesty", "compassion", "friendship" }
            ),
            // ── Punishment vs Redemption ─────────────────────────────────────────────
            (
                "A person who committed a serious crime 20 years ago has since become a model citizen and community pillar. They've never been caught.",
                "Should they turn themselves in?",
                new List<string>
                {
                    "Yes - justice requires it regardless of how much they've changed",
                    "No - they've served a moral sentence through their changed life",
                    "It depends on the nature of the crime and harm done",
                    "They should make restitution to victims privately without going to prison",
                    "Only if there are ongoing injustices from the original crime (e.g. an innocent person imprisoned)"
                },
                new List<string> { "justice", "punishment", "redemption" }
            ),
            // ── Collective vs Individual ─────────────────────────────────────────────
            (
                "During a pandemic, a vaccine is safe and effective. Some people refuse it on personal grounds, prolonging the crisis.",
                "Should vaccination be mandatory?",
                new List<string>
                {
                    "Yes - collective welfare overrides individual choice in crises",
                    "No - bodily autonomy is an absolute right",
                    "Strong incentives yes, mandates no",
                    "Depends on the severity and how much refusal is causing harm",
                    "Mandates breed distrust; better to invest in education and outreach"
                },
                new List<string> { "autonomy", "collectivism", "public-health" }
            ),
        };

        var pool = ExcludeAsked(dilemmas,
            profile.AskedQuestionHashes,
            d => d.Question,
            d => d.Options,
            d => d.Scenario);

        var unexploredPool = pool
            .Where(d => !d.Dimensions.All(dim => profile.ExploredDimensions.Contains(dim)))
            .ToList();
        if (!unexploredPool.Any()) unexploredPool = pool;

        var uncertainAreas = snapshot.Statistics.UncertainAreas;
        var prioritizedPool = unexploredPool
            .Where(d => d.Dimensions.Any(dim => uncertainAreas.Contains(dim)))
            .ToList();

        var selectedDilemma = prioritizedPool.Any()
            ? prioritizedPool[new Random().Next(prioritizedPool.Count)]
            : unexploredPool[new Random().Next(unexploredPool.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.MoralDilemma,
            Content = new InteractionContent
            {
                Context = selectedDilemma.Scenario,
                Question = selectedDilemma.Question,
                Format = InteractionFormat.MultipleChoice,
                Options = selectedDilemma.Options
            },
            TargetedDimensions = selectedDilemma.Dimensions
        };
    }

    /// <summary>
    /// Generate a scenario with multiple choice responses
    /// </summary>
    public UserInteraction GenerateScenarioMultipleChoice(UserProfile profile, BeliefSnapshot snapshot)
    {
        var scenarios = new List<(string Scenario, string Question, List<string> Options, List<string> Dimensions)>
        {
            (
                "You see a homeless person asking for money. They appear able-bodied.",
                "What's your first thought?",
                new List<string>
                {
                    "I want to help - everyone deserves compassion",
                    "They should get a job instead of begging",
                    "I wonder what circumstances led them here",
                    "I feel uncomfortable and try to avoid eye contact",
                    "I'd rather donate to organizations that help them"
                },
                new List<string> { "compassion", "judgment", "social-issues" }
            ),
            (
                "A coworker takes credit for your idea in a meeting. Everyone seems to believe them.",
                "How do you feel?",
                new List<string>
                {
                    "Angry and betrayed - I need to correct this immediately",
                    "Disappointed but I'll let it go to avoid conflict",
                    "Upset, but I'll address it with them privately later",
                    "Surprised they'd do that - maybe it was a misunderstanding",
                    "Focused on making sure it doesn't happen again"
                },
                new List<string> { "justice", "conflict-resolution", "self-advocacy" }
            ),
            (
                "Someone cuts in front of you in a long line at the store.",
                "What do you do?",
                new List<string>
                {
                    "Politely point out they cut in line",
                    "Say nothing but feel annoyed",
                    "Assume they didn't realize and give them the benefit of the doubt",
                    "Loudly call them out",
                    "Let it go - it's not worth the confrontation"
                },
                new List<string> { "fairness", "conflict-avoidance", "assertiveness" }
            ),
            (
                "You're at a restaurant and realize after leaving that they undercharged you significantly.",
                "What do you do?",
                new List<string>
                {
                    "Go back and pay the difference",
                    "Consider it their mistake and let it go",
                    "Feel guilty but convince myself it's not my responsibility",
                    "Only go back if it's convenient",
                    "Tip extra next time to balance it out"
                },
                new List<string> { "honesty", "responsibility", "convenience" }
            ),
            (
                "Your neighbor's music is too loud late at night. This is the third time this week.",
                "How do you handle it?",
                new List<string>
                {
                    "Knock on their door and politely ask them to turn it down",
                    "Call the police/building management",
                    "Suffer in silence to avoid confrontation",
                    "Make noise back to send a message",
                    "Leave a polite note on their door"
                },
                new List<string> { "conflict-resolution", "assertiveness", "community" }
            ),
            (
                "You see a parent yelling harshly at their young child in public. The child is crying.",
                "What's your reaction?",
                new List<string>
                {
                    "Intervene - no child should be treated that way",
                    "Feel uncomfortable but mind my own business",
                    "Judge the parent negatively but don't act",
                    "Feel empathy for the parent - they might be struggling",
                    "Try to distract/comfort the child somehow"
                },
                new List<string> { "intervention", "judgment", "compassion" }
            ),
            (
                "A friend asks to borrow money. You can afford it but they have a history of not paying back loans.",
                "What do you say?",
                new List<string>
                {
                    "Lend it anyway - they're my friend",
                    "Politely decline with an excuse",
                    "Give it as a gift with no expectation of return",
                    "Agree but only if they sign something or set clear terms",
                    "Honestly explain why I'm uncomfortable lending to them"
                },
                new List<string> { "friendship", "boundaries", "honesty" }
            ),
            (
                "You witness someone shoplifting food from a grocery store. They look desperate.",
                "What do you do?",
                new List<string>
                {
                    "Report it - stealing is wrong regardless",
                    "Pretend I didn't see it",
                    "Offer to buy the food for them instead",
                    "Feel conflicted but ultimately do nothing",
                    "Alert them that I saw, giving them a chance to stop"
                },
                new List<string> { "justice", "compassion", "rule-following" }
            ),
            // ── Digital & Social Media ───────────────────────────────────────────────
            (
                "You notice a friend sharing misinformation on social media. You know it's false but they seem to genuinely believe it.",
                "What do you do?",
                new List<string>
                {
                    "Correct them publicly on the post - truth matters",
                    "Message them privately with correct information",
                    "Ignore it - correcting people online rarely works",
                    "Report the post to the platform",
                    "Say nothing to preserve the friendship"
                },
                new List<string> { "epistemology", "honesty", "friendship" }
            ),
            (
                "You discover your teenage child has been secretly messaging someone much older online.",
                "What do you do?",
                new List<string>
                {
                    "Immediately confront them and block the contact without discussion",
                    "Have a calm, honest conversation about online safety first",
                    "Monitor the situation secretly before acting",
                    "Report the adult to authorities if anything seems inappropriate",
                    "Restrict their internet access until you understand the full picture"
                },
                new List<string> { "parenting", "autonomy", "security" }
            ),
            (
                "A close friend posts something on social media that you find deeply offensive. They don't seem to realize how it reads.",
                "How do you respond?",
                new List<string>
                {
                    "Comment publicly to address it",
                    "Send them a private message explaining how it lands",
                    "Unfollow or mute them quietly",
                    "Say nothing - it's their right to express themselves",
                    "It depends on whether it's offensive or just different from my views"
                },
                new List<string> { "free-speech", "friendship", "conflict-resolution" }
            ),
            // ── Workplace ────────────────────────────────────────────────────────────
            (
                "You overhear two colleagues discussing plans to start a competing business using your company's confidential methods.",
                "What do you do?",
                new List<string>
                {
                    "Report it to management immediately",
                    "Confront the colleagues directly first",
                    "Do nothing - it's not my business",
                    "Gather more evidence before deciding",
                    "Consult HR or legal counsel about my obligations"
                },
                new List<string> { "loyalty", "integrity", "civic-duty" }
            ),
            (
                "Your team is asked to do something legal but that you consider ethically questionable. The project pays your salary.",
                "What do you do?",
                new List<string>
                {
                    "Raise my concerns formally and refuse if they're not addressed",
                    "Comply while looking for another job",
                    "Comply - it's legal and they're paying me",
                    "Ask for reassignment to a different project",
                    "Complete it but find ways to minimize the harm within my role"
                },
                new List<string> { "integrity", "courage", "pragmatism" }
            ),
            // ── Community & Civic ────────────────────────────────────────────────────
            (
                "Your neighborhood association wants to block a homeless shelter from being built nearby.",
                "What's your position?",
                new List<string>
                {
                    "Oppose it - property values and safety concerns are real",
                    "Support it - people need shelter and we should be welcoming",
                    "Support it, but with proper management and support services",
                    "Let the neighborhood vote democratically",
                    "I'd want more information before taking a strong position"
                },
                new List<string> { "compassion", "community", "self-interest" }
            ),
            (
                "You find out a longtime respected community leader has privately held views you find repugnant, but they've never acted on them.",
                "How does this change your view of them?",
                new List<string>
                {
                    "It doesn't - actions and character matter more than private thoughts",
                    "It significantly damages my respect for them",
                    "It raises serious questions even if they've behaved well",
                    "It depends on the specific views and their role",
                    "I'd feel uncomfortable but try to separate the ideas from the person"
                },
                new List<string> { "moral-judgment", "integrity", "tolerance" }
            ),
            // ── Personal Health & Lifestyle ──────────────────────────────────────────
            (
                "A good friend has developed an unhealthy addiction that's damaging their life. They haven't asked for help.",
                "What do you do?",
                new List<string>
                {
                    "Say nothing - it's their life and they haven't asked",
                    "Tell them honestly how you see it, once",
                    "Involve family and stage an intervention",
                    "Keep inviting them to activities that don't involve the addiction",
                    "Be available but let them come to you when ready"
                },
                new List<string> { "autonomy", "compassion", "friendship" }
            ),
            (
                "You find out your doctor has a religious or philosophical objection to advising you on a legal medical procedure.",
                "How do you react?",
                new List<string>
                {
                    "I switch doctors - medical professionals must set aside personal views",
                    "I respect their belief but demand a referral to someone who will help",
                    "I respect their conscience - they shouldn't be forced to act against beliefs",
                    "Their personal views are irrelevant; they have a professional duty",
                    "It depends on how much their refusal affects access to care"
                },
                new List<string> { "religion", "autonomy", "professional-ethics" }
            ),
            // ── Global & Future ──────────────────────────────────────────────────────
            (
                "A technological breakthrough could give your country a decisive military and economic advantage, but requires a massive surveillance infrastructure.",
                "Should your government pursue it?",
                new List<string>
                {
                    "No - the surveillance infrastructure is too dangerous",
                    "Yes - national security and prosperity come first",
                    "Only with strong democratic oversight and sunset clauses",
                    "Only if rivals aren't likely to develop it first",
                    "The decision should be put to a public referendum"
                },
                new List<string> { "privacy", "security", "nationalism", "authority" }
            ),
            (
                "You can donate to a local charity helping people you can see, or to an international charity that saves more lives per dollar in a distant country.",
                "What do you choose?",
                new List<string>
                {
                    "The international charity - more lives saved is what matters",
                    "The local charity - I feel a stronger moral duty to my community",
                    "Split between both",
                    "The local charity - donor money is often wasted in distant contexts",
                    "The most effective one regardless of geography, once I've verified impact"
                },
                new List<string> { "compassion", "group-loyalty", "consequentialism" }
            ),
            // ── Family & Relationships ───────────────────────────────────────────────
            (
                "Your adult sibling makes a major life choice (career, partner, religion) you strongly disagree with.",
                "How do you handle it?",
                new List<string>
                {
                    "Express my concern clearly but ultimately respect their choice",
                    "Stay silent - it's their life",
                    "Try to persuade them to reconsider",
                    "Limit my relationship with them until they change course",
                    "Support them unconditionally - family is family"
                },
                new List<string> { "autonomy", "family", "values" }
            ),
            (
                "You inherit money on the condition that you follow a relative's wishes about how to spend it - wishes you disagree with.",
                "What do you do?",
                new List<string>
                {
                    "Follow the wishes exactly - it was their money with conditions",
                    "Use it as I see fit - they're gone and can't really know",
                    "Decline the inheritance rather than compromise my values",
                    "Fulfill the spirit if not the letter of their wishes",
                    "Seek legal advice on whether the conditions are binding"
                },
                new List<string> { "honesty", "autonomy", "rule-following" }
            ),
        };

        var pool = ExcludeAsked(scenarios,
            profile.AskedQuestionHashes,
            s => s.Question,
            s => s.Options,
            s => s.Scenario);

        var unexploredPool = pool
            .Where(s => !s.Dimensions.All(dim => profile.ExploredDimensions.Contains(dim)))
            .ToList();
        if (!unexploredPool.Any()) unexploredPool = pool;

        var uncertainAreas = snapshot.Statistics.UncertainAreas;
        var prioritizedPool = unexploredPool
            .Where(s => s.Dimensions.Any(dim => uncertainAreas.Contains(dim)))
            .ToList();

        var selectedScenario = prioritizedPool.Any()
            ? prioritizedPool[new Random().Next(prioritizedPool.Count)]
            : unexploredPool[new Random().Next(unexploredPool.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.ScenarioReaction,
            Content = new InteractionContent
            {
                Context = selectedScenario.Scenario,
                Question = selectedScenario.Question,
                Format = InteractionFormat.MultipleChoice,
                Options = selectedScenario.Options
            },
            TargetedDimensions = selectedScenario.Dimensions
        };
    }

    #region Private Helpers

    /// <summary>
    /// Computes the same hash used by BeliefDiscoveryOrchestrator to identify unique questions.
    /// </summary>
    private static string ComputeQuestionHash(string question, List<string>? options = null, string? context = null)
    {
        var content = question ?? "";
        if (options?.Any() == true)
            content += "|" + string.Join("|", options);
        if (!string.IsNullOrEmpty(context))
            content += "|" + context;
        return content.GetHashCode().ToString();
    }

    /// <summary>
    /// Filters a question pool to only those the user has not yet seen.
    /// Falls back to the full pool if every question has been asked.
    /// </summary>
    private static List<T> ExcludeAsked<T>(
        IEnumerable<T> pool,
        HashSet<string> askedHashes,
        Func<T, string> questionSelector,
        Func<T, List<string>?> optionsSelector,
        Func<T, string?> contextSelector)
    {
        var unseen = pool.Where(item =>
        {
            var hash = ComputeQuestionHash(
                questionSelector(item),
                optionsSelector(item),
                contextSelector(item));
            return !askedHashes.Contains(hash);
        }).ToList();

        return unseen.Count > 0 ? unseen : pool.ToList();
    }

    private DiscoveryStage DetermineStage(UserProfile profile)
    {
        var count = profile.InteractionCount;
        return count switch
        {
            < 5 => DiscoveryStage.Initial,
            < 15 => DiscoveryStage.Foundation,
            < 30 => DiscoveryStage.Exploration,
            < 60 => DiscoveryStage.Refinement,
            _ => DiscoveryStage.Continuous
        };
    }

    private string BuildQuestionPrompt(
        DiscoveryStage stage,
        List<string> uncertainAreas,
        List<string> previousQuestions,
        UserProfile profile)
    {
        // SIMPLIFIED for smaller models - just ask for a single thoughtful question
        var basePrompt = stage switch
        {
            DiscoveryStage.Initial => "Ask a thoughtful question about what someone values most in life.",
            DiscoveryStage.Foundation => "Ask a question about someone's views on human nature and society.",
            DiscoveryStage.Exploration => "Ask a moral dilemma question with no clear right answer.",
            DiscoveryStage.Refinement => "Ask a challenging question about ethics and values.",
            _ => "Ask an open-ended question about beliefs and worldview."
        };

        return basePrompt + " Keep it under 50 words.";
    }

    private UserInteraction ParseQuestionResponse(string questionData, string userId)
    {
        // For now, treat the entire response as an open-ended question
        // In production, you might parse structured JSON
        return new UserInteraction
        {
            UserId = userId,
            Type = InteractionType.OpenEndedQuestion,
            Content = new InteractionContent
            {
                Question = questionData.Trim(),
                Format = InteractionFormat.OpenText
            }
        };
    }

    private InteractionContent ParseDilemmaContent(string dilemmaText)
    {
        // Simple parsing - in production, use more robust parsing
        var lines = dilemmaText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var scenario = "";
        var question = "";

        foreach (var line in lines)
        {
            if (line.StartsWith("SCENARIO:", StringComparison.OrdinalIgnoreCase))
                scenario = line.Substring(9).Trim();
            else if (line.StartsWith("QUESTION:", StringComparison.OrdinalIgnoreCase))
                question = line.Substring(9).Trim();
            else if (string.IsNullOrEmpty(question))
                scenario += "\n" + line;
        }

        return new InteractionContent
        {
            Question = question,
            Context = scenario,
            Format = InteractionFormat.OpenText
        };
    }

    #endregion
}
