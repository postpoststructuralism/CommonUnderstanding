using CommonUnderstanding.Services.Social.Plugins;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using CommonUnderstanding.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommonUnderstanding.Tests.Services;

public sealed class ProposedRelationContainmentTests
{
    [Fact]
    public async Task SparseWorldviewReturnsDirectArgumentsWithoutModelsOrEmbeddings()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName).Options;
        var factory = new FixtureDbFactory(options);
        using var services = new ServiceCollection()
            .AddSingleton(new DatabaseProviderInfo(false))
            .AddDbContext<ApplicationDbContext>(builder => builder.UseInMemoryDatabase(databaseName))
            .BuildServiceProvider();
        await using var db = factory.CreateDbContext();
        db.UserProfiles.Add(new PersistedUserProfile { Id = "owner", Name = "Fixture" });
        db.Arguments.Add(new Argument { Id = 991, Title = "Direct fixture", RawText = "Direct claim", SubmittedBy = "owner" });
        await db.SaveChangesAsync();
        var store = new UserProfileStore(services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<UserProfileStore>.Instance);
        var service = new WorldviewInsightService(factory, store, null!, null!, null!);
        var result = await service.BuildAsync("owner");
        Assert.Equal("Direct claim", Assert.Single(result.Arguments).Claim);
        Assert.Empty(result.CanonicalMatches);
        Assert.Empty(result.Peers);
        Assert.Empty(result.CommunityViews);
        Assert.Contains("Insufficient information", result.Summary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistedLinksDoNotChangeRecommendations(bool indexedServing)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var factory = new FixtureDbFactory(options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Recommendation:UseIndexedServing"] = indexedServing.ToString()
            }).Build();
        var service = new FeedRankingService(factory, TimeProvider.System, null!, configuration,
            NullLogger<FeedRankingService>.Instance);
        var argumentId = Guid.NewGuid();
        await using var db = factory.CreateDbContext();
        db.SocialArguments.Add(new SocialArgument
        {
            Id = argumentId, UserId = "owner", Title = "Public fixture", WarrantText = "Fixture warrant", IsPublic = true,
            CreatedAt = DateTime.UtcNow, Tags = ["fixture"]
        });
        await db.SaveChangesAsync();
        var before = await service.GetFeedAsync(null, 0, 10, null, default);
        db.ArgumentLinks.Add(new ArgumentLink
        {
            SourceArgumentId = argumentId, TargetArgumentId = Guid.NewGuid(),
            UserId = "model", LinkType = LinkType.Contradicts
        });
        await db.SaveChangesAsync();
        var after = await service.GetFeedAsync(null, 0, 10, null, default);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before),
            System.Text.Json.JsonSerializer.Serialize(after));
        Assert.Single(await db.ArgumentLinks.ToListAsync());
    }

    private sealed class FixtureDbFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options, new DatabaseProviderInfo(false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public async Task ContainmentPreventsSuggestionReadsWritesAndModelWork(string? enabled)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AnalyticalContainment:Enabled"] = enabled }).Build();
        var plugin = new ArgumentLinkSuggestionPlugin(null!, null!, null!,
            NullLogger<ArgumentLinkSuggestionPlugin>.Instance, configuration);
        var suggestions = await plugin.SuggestLinksAsync(Guid.NewGuid());
        Assert.Empty(suggestions);
    }
}