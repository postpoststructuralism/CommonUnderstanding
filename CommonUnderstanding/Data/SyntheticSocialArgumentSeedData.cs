using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Data;

public static class SyntheticSocialArgumentSeedData
{
    private const string SystemUsername = "understanding_graph";

    public static async Task EnsureAsync(ApplicationDbContext db, ILogger logger)
    {
        var labels = SyntheticLabels.All;
        var argumentIds = labels.Select(SyntheticLabels.GetSocialArgumentId).ToArray();
        var existingIds = await db.SocialArguments
            .Where(argument => argumentIds.Contains(argument.Id))
            .Select(argument => argument.Id)
            .ToHashSetAsync();

        if (existingIds.Count == argumentIds.Length) return;

        var systemUser = await db.UserAccounts
            .FirstOrDefaultAsync(user => user.Username == SystemUsername);
        if (systemUser == null)
        {
            systemUser = new UserAccount
            {
                Id = "00000000-0000-0000-0000-000000000001",
                Username = SystemUsername,
                DisplayName = "Understanding Graph",
                PasswordHash = string.Empty,
                IsActive = false
            };
            db.UserAccounts.Add(systemUser);
        }

        foreach (var label in labels)
        {
            var argumentId = SyntheticLabels.GetSocialArgumentId(label);
            if (existingIds.Contains(argumentId)) continue;

            var propositionId = SyntheticLabels.GetClaimPropositionId(label);
            db.SocialPropositions.Add(new SocialProposition
            {
                Id = propositionId,
                Text = label,
                Type = SocialPropositionType.Claim,
                UserId = systemUser.Id,
                IsAIGenerated = true,
                IsConfirmed = true
            });
            db.SocialArguments.Add(new SocialArgument
            {
                Id = argumentId,
                Title = label,
                ClaimPropositionId = propositionId,
                WarrantText = "This proposition is represented as a topic in the understanding graph.",
                IsPublic = true,
                UserId = systemUser.Id,
                Tags = new[] { "understanding-graph" }
            });
            db.SocialArgumentPropositions.Add(new SocialArgumentProposition
            {
                ArgumentId = argumentId,
                PropositionId = propositionId,
                Role = SocialPropositionType.Claim,
                OrderIndex = 0
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Ensured backing social arguments for {Count} synthetic graph labels.", labels.Length);
    }
}