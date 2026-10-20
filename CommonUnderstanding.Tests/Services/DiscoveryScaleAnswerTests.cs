using CommonUnderstanding.Models;
using CommonUnderstanding.Controllers;
using CommonUnderstanding.Data;
using CommonUnderstanding.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace CommonUnderstanding.Tests.Services;

public sealed class DiscoveryScaleAnswerTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Care", false)]
    [InlineData("Care > Care", false)]
    [InlineData("Care > Unknown", false)]
    [InlineData("Care > Freedom", true)]
    [InlineData("Freedom > Care", true)]
    public void RankingsRequireEveryAvailableOptionExactlyOnce(string? response, bool valid)
    {
        var content = new InteractionContent { Options = ["Care", "Freedom"] };
        Assert.Equal(valid, content.IsValidRankingAnswer(response));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(0d, false)]
    [InlineData(11d, false)]
    [InlineData(1d, true)]
    [InlineData(5d, true)]
    [InlineData(10d, true)]
    public void OnlyExplicitFiniteInRangeAnswersAreValid(double? value, bool expected)
    {
        var content = new InteractionContent { MinValue = 1, MaxValue = 10 };
        Assert.Equal(expected, content.IsValidScaleAnswer(value));
    }

    [Fact]
    public void UndefinedAndReversedEndpointsFailClosed()
    {
        Assert.False(new InteractionContent().IsValidScaleAnswer(5));
        Assert.False(new InteractionContent { MinValue = 10, MaxValue = 1 }.IsValidScaleAnswer(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(11d)]
    [InlineData(null, true)]
    public async Task ControllerRejectedScalePreservesUnansweredInteraction(double? value, bool ranking = false)
    {
        var databaseName = Guid.NewGuid().ToString();
        using var services = new ServiceCollection()
            .AddSingleton(new DatabaseProviderInfo(isPostgres: false))
            .AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .BuildServiceProvider();
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserProfiles.Add(new PersistedUserProfile { Id = "owner", Name = "Fixture" });
            await db.SaveChangesAsync();
        }
        var store = new UserProfileStore(services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<UserProfileStore>.Instance);
        var profile = store.GetProfile("owner")!;
        var interaction = new UserInteraction
        {
            UserId = "owner",
            Content = new InteractionContent
            {
                Format = ranking ? InteractionFormat.Ranking : InteractionFormat.Scale,
                MinValue = 1, MaxValue = 10, Options = ["Care", "Freedom"]
            }
        };
        var originalResponse = interaction.Response;
        store.SetPendingInteraction("owner", interaction);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, "owner") }, "Test"))
        };
        var controller = new DiscoveryController(null!, store, null!, null!, null!, null!,
            NullLogger<DiscoveryController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new EmptyTempDataProvider())
        };
        var result = Assert.IsType<RedirectToActionResult>(await controller.SubmitResponse(null, null, null, value));
        Assert.IsType<NotFoundResult>(controller.Compare("owner", "other"));
        Assert.IsType<NotFoundResult>(controller.Compare("other", "owner"));
        Assert.IsType<ViewResult>(await controller.Profile());
        Assert.Equal("Question", result.ActionName);
        Assert.Same(interaction, store.GetPendingInteraction("owner"));
        Assert.Same(originalResponse, interaction.Response);
        Assert.Null(interaction.Response.NumericValue);
        Assert.Empty(interaction.Response.RawText);
        Assert.Empty(profile.Interactions);
        Assert.Equal(0, interaction.ResponseTimeMs);
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}