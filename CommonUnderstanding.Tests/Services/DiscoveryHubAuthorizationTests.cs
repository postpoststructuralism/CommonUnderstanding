using System.Security.Claims;
using CommonUnderstanding.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommonUnderstanding.Tests.Services;

public sealed class DiscoveryHubAuthorizationTests
{
    [Theory]
    [InlineData(false, "owner")]
    [InlineData(true, "other")]
    [InlineData(true, "")]
    public async Task UnownedProfilesFailBeforeAnyDependenciesAreUsed(bool authenticated, string requestedProfile)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "owner") },
            authenticated ? "Test" : null);
        var hub = new DiscoveryHub(null!, null!, null!, null!, null!, NullLogger<DiscoveryHub>.Instance)
        {
            Context = new CallerContext(new ClaimsPrincipal(identity))
        };
        await Assert.ThrowsAsync<HubException>(() => hub.StreamQuestion(requestedProfile));
        await Assert.ThrowsAsync<HubException>(() => hub.ProcessResponseStreaming(requestedProfile, null, null, null, null));
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(DiscoveryHub), typeof(AuthorizeAttribute)));
        if (!authenticated)
            await Assert.ThrowsAsync<HubException>(() => hub.StartDiscovery());
    }

    private sealed class CallerContext(ClaimsPrincipal user) : HubCallerContext
    {
        public override string ConnectionId => "fixture";
        public override string? UserIdentifier => user.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}