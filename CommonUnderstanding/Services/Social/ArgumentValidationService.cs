using CommonUnderstanding.Models.Social;
using Microsoft.Extensions.Logging;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Service for validating social arguments
/// </summary>
public class ArgumentValidationService
{
    private readonly ILogger<ArgumentValidationService> _logger;

    public ArgumentValidationService(ILogger<ArgumentValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate a social argument before saving
    /// </summary>
    public async Task ValidateSocialArgumentAsync(SocialArgument argument, CancellationToken ct = default)
    {
        if (argument == null)
            throw new ArgumentNullException(nameof(argument));

        // Validate required fields
        if (string.IsNullOrWhiteSpace(argument.Title))
            throw new ArgumentException("Argument title is required");
        
        if (argument.Title.Length > 300)
            throw new ArgumentException("Argument title cannot exceed 300 characters");

        if (string.IsNullOrWhiteSpace(argument.WarrantText))
            throw new ArgumentException("Warrant text is required");
        
        if (argument.WarrantText.Length > 5000)
            throw new ArgumentException("Warrant text cannot exceed 5000 characters");

        if (argument.ResolutionText?.Length > 2000)
            throw new ArgumentException("Resolution text cannot exceed 2000 characters");

        // Validate tags
        if (argument.Tags != null)
        {
            foreach (var tag in argument.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    throw new ArgumentException("Tags cannot be empty");
                
                if (tag.Length > 50)
                    throw new ArgumentException($"Tag '{tag}' cannot exceed 50 characters");
                
                if (!tag.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' '))
                    throw new ArgumentException($"Tag '{tag}' contains invalid characters");
            }
        }

        // Validate Schwartz values
        if (argument.SchwartzValues != null)
        {
            var validValues = new[] { "Self-Direction", "Stimulation", "Hedonism", "Achievement", 
                "Power", "Security", "Conformity", "Tradition", "Benevolence", "Universalism" };
            
            foreach (var value in argument.SchwartzValues)
            {
                if (!validValues.Contains(value))
                    throw new ArgumentException($"Invalid Schwartz value: {value}");
            }
        }

        _logger.LogDebug("Validated social argument: {Title}", argument.Title);
    }

    /// <summary>
    /// Check if argument content is appropriate (basic content moderation)
    /// </summary>
    public async Task<bool> IsContentAppropriateAsync(string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return true;

        // Basic profanity filter (in production, use a proper content moderation service)
        var inappropriateWords = new[] { "badword1", "badword2", "badword3" }; // Placeholder
        
        foreach (var word in inappropriateWords)
        {
            if (content.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Content contains inappropriate word: {Word}", word);
                return false;
            }
        }

        // Check for excessive capitalization (shouting)
        var upperCaseRatio = content.Count(char.IsUpper) / (double)content.Length;
        if (upperCaseRatio > 0.7 && content.Length > 20)
        {
            _logger.LogWarning("Content appears to be shouting (excessive capitalization)");
            return false;
        }

        // Check for repetitive characters
        if (ContainsExcessiveRepetition(content))
        {
            _logger.LogWarning("Content contains excessive repetition");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate that a user can create a follow-up argument
    /// </summary>
    public async Task<bool> CanCreateFollowUpAsync(string userId, Guid parentArgumentId, CancellationToken ct = default)
    {
        // Check if user is blocked from the parent argument
        // Check if user has been rate limited
        // Check if parent argument accepts replies
        // etc.
        
        return true; // Placeholder
    }

    private bool ContainsExcessiveRepetition(string content)
    {
        if (content.Length < 10)
            return false;

        // Check for repeated characters (e.g., "!!!!!!" or "aaaaaa")
        for (int i = 0; i < content.Length - 5; i++)
        {
            var segment = content.Substring(i, 6);
            if (segment.Distinct().Count() == 1)
                return true;
        }

        // Check for repeated words (e.g., "spam spam spam")
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 3)
        {
            for (int i = 0; i < words.Length - 2; i++)
            {
                if (words[i] == words[i + 1] && words[i] == words[i + 2])
                    return true;
            }
        }

        return false;
    }
}