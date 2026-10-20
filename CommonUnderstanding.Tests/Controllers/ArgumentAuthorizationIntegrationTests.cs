using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using CommonUnderstanding.Models;
using CommonUnderstanding.Controllers;
using CommonUnderstanding.Data;
using CommonUnderstanding.Services.Provenance;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace CommonUnderstanding.Tests.Controllers;

public sealed class ArgumentAuthorizationIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly Dictionary<string, string?> _configuration = new();
    private readonly CapturingLoggerProvider _loggerProvider = new();
    private IHost _host = null!;
    private HttpClient _client = null!;
    private bool _useFakeAnalysis;
    private readonly TaskCompletionSource _adjudicationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseAdjudication = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_configuration))
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase(_databaseName));
                    services.AddSingleton(new DatabaseProviderInfo(isPostgres: false));
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                    services.Configure<AuthenticationOptions>(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    });
                    services.AddAuthorization();
                    services.AddSingleton(_loggerProvider);
                    services.AddLogging(builder => builder.AddProvider(_loggerProvider));
                    services.AddAntiforgery(options => options.Cookie.Name = "Test.Antiforgery");
                    services.AddControllersWithViews()
                        .AddApplicationPart(typeof(ArgumentController).Assembly)
                        .ConfigureApplicationPartManager(manager =>
                            manager.FeatureProviders.Add(new ReportFixtureFeatureProvider()));
                    services.Replace(ServiceDescriptor.Transient<IControllerActivator,
                        RegisteredControllerActivator>());
                    services.AddScoped<ArgumentController>(serviceProvider => new ArgumentController(
                        serviceProvider.GetRequiredService<ApplicationDbContext>(),
                        _useFakeAnalysis ? new FixtureDecomposition() : null!,
                        _useFakeAnalysis ? new FixtureValidation() : null!,
                        _useFakeAnalysis ? new FixtureAdjudication(_adjudicationStarted, _releaseAdjudication) : null!,
                        null!, null!, null!, null!, null!, null!, null!,
                        new CommonUnderstanding.Services.StakeholderService(
                            serviceProvider.GetRequiredService<ApplicationDbContext>(),
                            NullLogger<CommonUnderstanding.Services.StakeholderService>.Instance),
                        new CommonUnderstanding.Services.DecisionSupportService(
                            serviceProvider.GetRequiredService<ApplicationDbContext>(),
                            new CommonUnderstanding.Services.StakeholderService(
                                serviceProvider.GetRequiredService<ApplicationDbContext>(),
                                NullLogger<CommonUnderstanding.Services.StakeholderService>.Instance),
                            NullLogger<CommonUnderstanding.Services.DecisionSupportService>.Instance),
                        null!, null!, null!,
                        serviceProvider.GetRequiredService<ILogger<ArgumentController>>()));
                    services.AddScoped<EmergentConclusionsController>(_ => new EmergentConclusionsController(
                        null!, null!, NullLogger<EmergentConclusionsController>.Instance));
                    services.AddScoped<ReportFixtureController>();
                    services.AddSingleton<CommonUnderstanding.Services.IEmergentFindingActionService,
                        CommonUnderstanding.Services.EmergentFindingActionService>();
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                        endpoints.MapGet("/__test/antiforgery", async context =>
                        {
                            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                            var tokens = antiforgery.GetAndStoreTokens(context);
                            context.Response.ContentType = "application/json";
                            await JsonSerializer.SerializeAsync(context.Response.Body,
                                new { tokens.RequestToken });
                        });
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Arguments.Add(new Argument
        {
            Id = 144,
            Title = "Private argument",
            RawText = "A private argument owned by one account.",
            SubmittedBy = "owner",
            Status = ArgumentStatus.Draft
        });
        db.Arguments.Add(new Argument
        {
            Id = 145,
            Title = "Completed argument",
            RawText = "A completed argument with no linked graph agreement.",
            SubmittedBy = "owner",
            Status = ArgumentStatus.Complete
        });
        db.Arguments.Add(new Argument
        {
            Id = 146,
            Title = "Second completed argument",
            RawText = "A second completed argument for comparison.",
            SubmittedBy = "owner",
            Status = ArgumentStatus.Complete
        });
        db.ArgumentComparisons.Add(new ArgumentComparison
        {
            Id = 1,
            ArgumentAId = 145,
            ArgumentBId = 146,
            ConflictingPremisesJson = "[{\"PremiseA\":\"A disputed premise\",\"PremiseB\":\"Its opposite\",\"Explanation\":\"AI conflict judgment\"}]",
            ComplementaryPremisesJson = "[\"AI shared-ground judgment\"]",
            UniqueToPremisesAJson = "[\"AI unique claim judgment\"]",
            UniqueToPremisesBJson = "[\"AI second unique judgment\"]",
            SynthesisNarrative = "AI synthesis narrative",
            NetDirection = NetDirection.FavoursA,
            NetConfidence = 0.91
        });
        db.Claims.Add(new CommonUnderstanding.Models.Claim
        {
            Id = 1450,
            ArgumentId = 145,
            Text = "The completed argument claim.",
            Premises =
            {
                new Proposition
                {
                    Id = 1451,
                    Text = "An AI-only settled premise.",
                    Status = PropositionStatus.Settled,
                    ProvisionalAssessment = "The model considers this likely."
                }
            }
        });
        db.StakeholderPositions.Add(new StakeholderPosition
        {
            StakeholderId = 1,
            ArgumentId = 145,
            Position = StakeholderPositionType.Undecided
        });
        db.CommonUnderstandingNodes.Add(new CommonUnderstandingNode
        {
            Text = "Unrelated global proposition",
            NormalizedKey = "unrelated global proposition",
            Status = PropositionStatus.Settled,
            Confidence = 0.9
        });
        db.CommonUnderstandingNodes.Add(new CommonUnderstandingNode
        {
            Text = "A graph node matching the argument premise",
            NormalizedKey = "an ai-only settled premise",
            ArgumentIdsJson = "[999]",
            Status = PropositionStatus.Settled,
            Confidence = 0.9
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _host.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Delete_DeniesAuthenticatedNonOwner_AndPreservesArgument()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/Delete/144")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>())
        };
        request.Headers.Add("X-Test-User", "different-user");
        await AddAntiforgeryTokenAsync(request);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull(await db.Arguments.FindAsync(144));
    }

    [Fact]
    public async Task Delete_AllowsOwnerWithAntiforgeryToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/Delete/144")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>())
        };
        request.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(request);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null(await db.Arguments.FindAsync(144));
    }

    [Fact]
    public async Task Delete_RejectsUnauthenticatedRequest()
    {
        using var response = await _client.PostAsync("/Argument/Delete/144", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull(await db.Arguments.FindAsync(144));
    }

    [Fact]
    public async Task Delete_RejectsMissingAntiforgeryToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/Delete/144")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>())
        };
        request.Headers.Add("X-Test-User", "owner");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull(await db.Arguments.FindAsync(144));
    }

    [Theory]
    [InlineData("/Argument/Analyze/144")]
    [InlineData("/Argument/WhatChanged/144")]
    [InlineData("/Argument/Review/144")]
    [InlineData("/Argument/View/144")]
    [InlineData("/Argument/DecisionSupport/144")]
    public async Task DirectArgumentReads_HideOwnedRecordsFromOtherUsers(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-User", "different-user");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WhatChanged_DoesNotAttributeGlobalSettledNodesAsAgreement()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Argument/WhatChanged/145");
        request.Headers.Add("X-Test-User", "owner");

        using var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Areas of Agreement", content);
        Assert.DoesNotContain("Unrelated global proposition", content);
    }

    [Fact]
    public async Task WhatChanged_ContainsGraphAttributionsByDefault_AndRestoresThemWhenDisabled()
    {
        using var enabledRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/WhatChanged/145");
        enabledRequest.Headers.Add("X-Test-User", "owner");

        using var enabledResponse = await _client.SendAsync(enabledRequest);
        var enabledContent = await enabledResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        Assert.Contains("Map impact and relationships have not been determined", enabledContent);
        Assert.Contains("Extracted Claims for Review", enabledContent);
        Assert.Contains("Unreviewed draft", enabledContent);
        Assert.DoesNotContain("A graph node matching the argument premise", enabledContent);
        Assert.DoesNotContain("Existing Propositions Strengthened", enabledContent);

        _host.Services.GetRequiredService<IConfiguration>()["AnalyticalContainment:Enabled"] = "false";

        using var disabledRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/WhatChanged/145");
        disabledRequest.Headers.Add("X-Test-User", "owner");
        using var disabledResponse = await _client.SendAsync(disabledRequest);
        var disabledContent = await disabledResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        Assert.Contains("Existing Propositions Strengthened", disabledContent);
        Assert.Contains("A graph node matching the argument premise", disabledContent);
    }

    [Fact]
    public async Task Publish_DirectPostCannotPublishWithoutReviewSubmission()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/Publish/145")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>())
        };
        request.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(request);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Review", response.Headers.Location?.ToString());
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(await db.SocialArguments.ToListAsync(), _ => true);
        Assert.DoesNotContain(_loggerProvider.Entries,
            item => item.Message.Contains("Argument workflow publication", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReviewPost_PublishesTheClaimTextExplicitlySubmittedByOwner()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/Review/145")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Action"] = "publish",
                ["ClaimText"] = "The claim I reviewed and approved."
            })
        };
        request.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(request);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var published = await db.SocialArguments
            .Include(argument => argument.ClaimProposition)
            .SingleAsync(argument => argument.SourceArgumentId == 145);
        Assert.True(published.IsPublic);
        Assert.True(published.ClaimProposition!.IsConfirmed);
        Assert.Equal("The claim I reviewed and approved.", published.ClaimProposition.Text);
        var entry = Assert.Single(_loggerProvider.Entries,
            item => item.Message.Contains("Argument workflow publication", StringComparison.Ordinal));
        Assert.Equal("publication", entry.Properties["EventName"]);
        Assert.Equal("publish", entry.Properties["PublicationAction"]);
        Assert.Equal(145, entry.Properties["ArgumentId"]);
        Assert.Equal(published.Id, entry.Properties["SocialArgumentId"]);
        Assert.DoesNotContain("The claim I reviewed and approved.", entry.Message);
    }

    [Fact]
    public async Task Review_LogsStructuredActionWithoutClaimContent()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/Review/145")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Action"] = "edit",
                ["ClaimText"] = "A corrected private claim",
                ["ClaimType"] = "empirical"
            })
        };
        request.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(request);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var entry = Assert.Single(_loggerProvider.Entries,
            item => item.Message.Contains("Argument workflow review", StringComparison.Ordinal));
        Assert.Equal("review", entry.Properties["EventName"]);
        Assert.Equal("edit", entry.Properties["ReviewerAction"]);
        Assert.Equal("owner", entry.Properties["ReviewerId"]);
        Assert.False(string.IsNullOrWhiteSpace(entry.Properties["RequestCorrelationId"]?.ToString()));
        Assert.Null(entry.Properties["AnalysisJobId"]);
        Assert.Null(entry.Properties["ProjectId"]);
        Assert.DoesNotContain("A corrected private claim", entry.Message);
    }

    [Fact]
    public async Task AnalysisFailureLogsLifecycleWithoutPrivateContentAndRestoresDraft()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/AnalyzeStream/144");
        request.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(request);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var eventName in new[] { "analysis_start", "analysis_fail" })
        {
            var entry = Assert.Single(_loggerProvider.Entries,
                item => Equals(item.Properties.GetValueOrDefault("EventName"), eventName));
            Assert.Equal("owner", entry.Properties["ActorId"]);
            Assert.Equal(144, entry.Properties["ArgumentId"]);
            Assert.Null(entry.Properties["AnalysisJobId"]);
            Assert.False(string.IsNullOrWhiteSpace(entry.Properties["RequestCorrelationId"]?.ToString()));
            Assert.DoesNotContain("A private argument owned by one account.", entry.Message);
        }
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(ArgumentStatus.Draft, (await db.Arguments.SingleAsync(argument => argument.Id == 144)).Status);
        Assert.Empty(await db.SocialArguments.ToListAsync());
    }

    [Fact]
    public async Task FakeCompletionCannotOverwriteCorrectionsOrPublish()
    {
        _useFakeAnalysis = true;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/AnalyzeStream/144");
        request.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(request);
        var completion = _client.SendAsync(request);
        await _adjudicationStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            using var correction = new HttpRequestMessage(HttpMethod.Post, "/Argument/Review/144")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                { ["Action"] = "edit", ["ClaimText"] = "Correction while analysis is active" })
            };
            correction.Headers.Add("X-Test-User", "owner");
            await AddAntiforgeryTokenAsync(correction);
            using var pendingResponse = await _client.SendAsync(correction);
            Assert.Equal(HttpStatusCode.Redirect, pendingResponse.StatusCode);
            Assert.Contains("/Argument/Analyze/144", pendingResponse.Headers.Location?.ToString());
        }
        finally
        {
            _releaseAdjudication.TrySetResult();
        }
        using var completedResponse = await completion;
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        using var save = new HttpRequestMessage(HttpMethod.Post, "/Argument/Review/144")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Action"] = "edit", ["ClaimText"] = "Saved correction after completion" })
        };
        save.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(save);
        using var savedResponse = await _client.SendAsync(save);
        Assert.Equal(HttpStatusCode.Redirect, savedResponse.StatusCode);
        using var late = new HttpRequestMessage(HttpMethod.Post, "/Argument/AnalyzeStream/144?force=true");
        late.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(late);
        using var lateResponse = await _client.SendAsync(late);
        Assert.Equal(HttpStatusCode.OK, lateResponse.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Saved correction after completion", (await db.Arguments.Include(argument => argument.Claims)
            .SingleAsync(argument => argument.Id == 144)).Claims.Single().Text);
        Assert.Empty(await db.SocialArguments.ToListAsync());
        var entry = Assert.Single(_loggerProvider.Entries,
            item => Equals(item.Properties.GetValueOrDefault("EventName"), "analysis_complete"));
        Assert.Equal("owner", entry.Properties["ActorId"]);
        Assert.Null(entry.Properties["AnalysisJobId"]);
        Assert.DoesNotContain("Synthetic claim", entry.Message);
        Assert.Single(_loggerProvider.Entries,
            item => Equals(item.Properties.GetValueOrDefault("EventName"), "analysis_start"));
    }

    [Fact]
    public async Task Review_SaveCorrectionsReturnsToEditableReviewWithoutPublication()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/Review/145")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Action"] = "edit",
                ["ClaimText"] = "A correction awaiting explicit publication"
            })
        };
        request.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(request);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Argument/Review/145", response.Headers.Location?.ToString());
        using var reviewRequest = new HttpRequestMessage(HttpMethod.Get, response.Headers.Location);
        reviewRequest.Headers.Add("X-Test-User", "owner");
        using var reviewResponse = await _client.SendAsync(reviewRequest);
        var content = await reviewResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        Assert.Contains("A correction awaiting explicit publication", content);
        Assert.Contains("id=\"claimText\"", content);
        Assert.DoesNotContain("getElementById('reviewForm').submit()", content);
        using var completionRequest = new HttpRequestMessage(HttpMethod.Post, "/Argument/AnalyzeStream/145?force=true");
        completionRequest.Headers.Add("X-Test-User", "owner");
        await AddAntiforgeryTokenAsync(completionRequest);
        using var completionResponse = await _client.SendAsync(completionRequest);
        Assert.Equal(HttpStatusCode.OK, completionResponse.StatusCode);
        Assert.Contains("/Argument/Review/145", await completionResponse.Content.ReadAsStringAsync());
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.SocialArguments.ToListAsync());
        Assert.Equal("A correction awaiting explicit publication",
            (await db.Arguments.Include(argument => argument.Claims).SingleAsync(argument => argument.Id == 145))
            .Claims.Single().Text);
    }

    [Fact]
    public async Task ArgumentView_ShowsLiteralPositionCountsWithoutDeclaringConsensus()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Argument/View/145");
        request.Headers.Add("X-Test-User", "owner");

        using var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Support: 0", content);
        Assert.Contains("Oppose: 0", content);
        Assert.Contains("Undecided: 1", content);
        Assert.DoesNotContain("Consensus reached", content);
        Assert.DoesNotContain("Majority position:", content);
        Assert.DoesNotContain("Consensus:", content);
    }

    [Fact]
    public async Task ArgumentView_ContainsAiOnlySettledLabelByDefault_AndRestoresItWhenDisabled()
    {
        using var enabledRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/View/145");
        enabledRequest.Headers.Add("X-Test-User", "owner");

        using var enabledResponse = await _client.SendAsync(enabledRequest);
        var enabledContent = await enabledResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        Assert.Contains("Unreviewed", enabledContent);
        Assert.Contains("badge bg-secondary small ms-2\">Unreviewed</span>", enabledContent);
        Assert.DoesNotContain(">Settled</span>", enabledContent);

        _host.Services.GetRequiredService<IConfiguration>()["AnalyticalContainment:Enabled"] = "false";

        using var disabledRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/View/145");
        disabledRequest.Headers.Add("X-Test-User", "owner");
        using var disabledResponse = await _client.SendAsync(disabledRequest);
        var disabledContent = await disabledResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        Assert.DoesNotContain("Unreviewed", disabledContent);
    }

    [Fact]
    public async Task DecisionSupport_ContainsRecommendationByDefault_AndRestoresItWhenDisabled()
    {
        using var enabledRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/DecisionSupport/145");
        enabledRequest.Headers.Add("X-Test-User", "owner");

        using var enabledResponse = await _client.SendAsync(enabledRequest);
        var enabledContent = await enabledResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        Assert.Contains("Decision recommendations are unavailable in conservative review mode", enabledContent);
        Assert.Contains("AI-generated draft for human review; not an evidence or consensus determination.", enabledContent);
        Assert.DoesNotContain("Recommendation:", enabledContent);
        Assert.DoesNotContain("Evidence Confidence", enabledContent);

        _host.Services.GetRequiredService<IConfiguration>()["AnalyticalContainment:Enabled"] = "false";

        using var disabledRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/DecisionSupport/145");
        disabledRequest.Headers.Add("X-Test-User", "owner");
        using var disabledResponse = await _client.SendAsync(disabledRequest);
        var disabledContent = await disabledResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        Assert.DoesNotContain("AI-generated draft for human review; not an evidence or consensus determination.", disabledContent);
        Assert.Contains("Recommendation:", disabledContent);
        Assert.Contains("Evidence Confidence", disabledContent);
    }

    [Fact]
    public async Task CompareView_ContainsJudgmentsByDefault_AndRestoresThemWhenDisabled()
    {
        using var enabledRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/CompareView/1");
        enabledRequest.Headers.Add("X-Test-User", "owner");

        using var enabledResponse = await _client.SendAsync(enabledRequest);
        var enabledContent = await enabledResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        Assert.Contains("Comparison judgments are unavailable in conservative review mode", enabledContent);
        Assert.Contains("Potential differences", enabledContent);
        Assert.DoesNotContain("Favours: Completed argument", enabledContent);
        Assert.DoesNotContain("AI conflict judgment", enabledContent);
        Assert.DoesNotContain("AI shared-ground judgment", enabledContent);
        Assert.DoesNotContain("AI unique claim judgment", enabledContent);
        Assert.DoesNotContain("AI synthesis narrative", enabledContent);

        _host.Services.GetRequiredService<IConfiguration>()["AnalyticalContainment:Enabled"] = "false";

        using var disabledRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/CompareView/1");
        disabledRequest.Headers.Add("X-Test-User", "owner");
        using var disabledResponse = await _client.SendAsync(disabledRequest);
        var disabledContent = await disabledResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        Assert.Contains("Favours: Completed argument", disabledContent);
        Assert.Contains("AI conflict judgment", disabledContent);
        Assert.Contains("AI shared-ground judgment", disabledContent);
        Assert.Contains("AI unique claim judgment", disabledContent);
        Assert.Contains("AI synthesis narrative", disabledContent);
    }

    [Fact]
    public async Task AnalyzeStream_RejectsGet()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Argument/AnalyzeStream/144");
        request.Headers.Add("X-Test-User", "owner");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeStream_RejectsPostWithoutAntiforgeryToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/AnalyzeStream/144")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["force"] = "false" })
        };
        request.Headers.Add("X-Test-User", "owner");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(ArgumentStatus.Draft, (await db.Arguments.FindAsync(144))?.Status);
    }

    [Fact]
    public async Task AnalyzeStream_HidesOwnedRecordBeforeStartingPostStream()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Argument/AnalyzeStream/144")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["force"] = "false" })
        };
        request.Headers.Add("X-Test-User", "different-user");
        await AddAntiforgeryTokenAsync(request);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var argument = await db.Arguments.FindAsync(144);
        Assert.Equal(ArgumentStatus.Draft, argument?.Status);
    }

    [Theory]
    [InlineData("/EmergentConclusions/DeepAnalyzeStream")]
    [InlineData("/EmergentConclusions/Export")]
    [InlineData("/EmergentConclusions/Refresh")]
    public async Task EmergentAnalysisGeneration_RejectsGet(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("/EmergentConclusions", "GET")]
    [InlineData("/EmergentConclusions/History", "GET")]
    [InlineData("/EmergentConclusions/ShowDeepReport/1", "GET")]
    [InlineData("/EmergentConclusions/GenerateDialogue", "POST")]
    [InlineData("/EmergentConclusions/DeepAnalysis", "GET")]
    [InlineData("/EmergentConclusions/Refresh", "POST")]
    [InlineData("/EmergentConclusions/DeepAnalyzeStream", "POST")]
    [InlineData("/EmergentConclusions/Export", "POST")]
    public async Task EmergentAnalysisActions_RejectUnauthenticatedRequests(string path, string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>())
        };

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/EmergentConclusions/Refresh")]
    [InlineData("/EmergentConclusions/DeepAnalyzeStream")]
    [InlineData("/EmergentConclusions/Export")]
    public async Task EmergentAnalysisActions_RejectAuthenticatedRequestsWithoutAntiforgery(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>())
        };
        request.Headers.Add("X-Test-User", "owner");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateDialogue_RejectsAuthenticatedRequestWithoutAntiforgery()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/EmergentConclusions/GenerateDialogue")
        {
            Content = JsonContent.Create(new { harmonyTitle = "Shared concern" })
        };
        request.Headers.Add("X-Test-User", "owner");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task AddAntiforgeryTokenAsync(HttpRequestMessage request)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/__test/antiforgery");
        if (request.Headers.TryGetValues("X-Test-User", out var userIds))
            tokenRequest.Headers.Add("X-Test-User", userIds);
        using var tokenResponse = await _client.SendAsync(tokenRequest);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryToken>();
        Assert.NotNull(tokenPayload?.RequestToken);

        var cookieHeader = string.Join("; ", tokenResponse.Headers.GetValues(HeaderNames.SetCookie)
            .Select(setCookie => setCookie.Split(';', 2)[0]));
        request.Headers.Add(HeaderNames.Cookie, cookieHeader);
        var formFields = request.Content is FormUrlEncodedContent existingForm
            ? (await existingForm.ReadAsStringAsync())
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => WebUtility.UrlDecode(pair[0])!,
                    pair => WebUtility.UrlDecode(pair.ElementAtOrDefault(1) ?? string.Empty)!)
            : new Dictionary<string, string>();
        formFields["__RequestVerificationToken"] = tokenPayload.RequestToken;
        request.Content = new FormUrlEncodedContent(formFields);
    }

    private sealed record AntiforgeryToken(string? RequestToken);

    [Fact]
    public async Task EmergentReportContainsHarmonySummaryAndCountsWithRestorationWhenDisabled()
    {
        using var enabledResponse = await _client.GetAsync("/__test/report");
        var enabledContent = await enabledResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        Assert.DoesNotContain("Unsupported harmony fixture", enabledContent);
        Assert.DoesNotContain("Unsupported consensus fixture", enabledContent);
        Assert.DoesNotContain("Unsupported summary fixture", enabledContent);
        Assert.Contains("cu-metric-value text-primary\">0</div>", enabledContent);
        Assert.Contains("cu-metric-value text-success\">0</div>", enabledContent);
        Assert.DoesNotContain("Average confidence:", enabledContent);

        _host.Services.GetRequiredService<IConfiguration>()["AnalyticalContainment:Enabled"] = "false";
        using var disabledResponse = await _client.GetAsync("/__test/report");
        var disabledContent = await disabledResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        Assert.Contains("Unsupported harmony fixture", disabledContent);
        Assert.Contains("Unsupported consensus fixture", disabledContent);
        Assert.Contains("Unsupported summary fixture", disabledContent);
        Assert.Contains("cu-metric-value text-primary\">2</div>", disabledContent);
        Assert.Contains("cu-metric-value text-success\">2</div>", disabledContent);
    }

    public sealed class ReportFixtureController : Controller
    {
        [HttpGet("/__test/map")]
        public IActionResult Map()
        {
            HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()
                .WebRootPath = Path.GetTempPath();
            return View("~/Views/UnderstandingGraph/Index.cshtml");
        }

        [HttpGet("/__test/node")]
        public IActionResult Node()
        {
            ViewBag.Node = new UnderstandingNode
            {
                CanonicalText = "Fixture node", Status = PropositionStatus.Settled, Confidence = 0.99
            };
            return View("~/Views/UnderstandingGraph/Node.cshtml");
        }

        [HttpGet("/__test/composer")]
        public IActionResult Composer() => View("~/Views/Social/WorldviewComposer.cshtml",
            new CommonUnderstanding.Models.Social.WorldviewInsight
            {
                HasBeliefProfile = true, Summary = "Unsupported composer portrait fixture"
            });

        [HttpGet("/__test/question/{format}")]
        public IActionResult Question(string format)
        {
            ViewBag.Profile = new UserProfile { Name = "Fixture" };
            ViewBag.ProgressPercent = 0;
            return View("~/Views/Discovery/Question.cshtml", new UserInteraction
            {
                Content = new InteractionContent
                {
                    Question = "Fixture response", Format = format == "scale" ? InteractionFormat.Scale : InteractionFormat.Ranking,
                    MinValue = 1, MaxValue = 10, MinLabel = "Minimum fixture", MaxLabel = "Maximum fixture",
                    Options = ["Care", "Freedom"]
                }
            });
        }

        [HttpGet("/__test/profile")]
        public IActionResult Profile()
        {
            ViewBag.AiSummary = "Unsupported sparse portrait fixture";
            return View("~/Views/Discovery/Profile.cshtml", new UserProfile
            {
                Name = "Fixture",
                CurrentBeliefSnapshot = new BeliefSnapshot { OverallConfidence = 0.99 }
            });
        }

        [HttpGet("/__test/report")]
        public IActionResult Report() => View("~/Views/EmergentConclusions/Index.cshtml",
            new EmergentConclusionsReport
            {
                HasSufficientData = true,
                ExecutiveSummary = "Unsupported summary fixture",
                Blindspots = [new EmergentConclusion
                {
                    Category = EmergentCategory.EmergentConsensus,
                    Title = "Unsupported consensus fixture", Significance = 0.9
                }],
                Harmonies = [new EmergentConclusion
                {
                    Category = EmergentCategory.ComplementaryChains,
                    Title = "Unsupported harmony fixture", Significance = 0.9
                }]
            });
    }

    [Fact]
    public async Task MapAndComposerContainUnsupportedAssessmentsWithRestorationWhenDisabled()
    {
        foreach (var surface in new[] { "map", "node", "composer" })
        {
            _host.Services.GetRequiredService<IConfiguration>()["AnalyticalContainment:Enabled"] = "true";
            using var enabledResponse = await _client.GetAsync($"/__test/{surface}");
            var enabledContent = await enabledResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
            Assert.Contains(surface == "composer" ? "Insufficient information" : "Unreviewed", enabledContent);
            Assert.DoesNotContain("Unsupported composer portrait fixture", enabledContent);
            if (surface == "node") Assert.DoesNotContain("<div class=\"label\">Confidence</div>", enabledContent);

            _host.Services.GetRequiredService<IConfiguration>()["AnalyticalContainment:Enabled"] = "false";
            using var disabledResponse = await _client.GetAsync($"/__test/{surface}");
            var disabledContent = await disabledResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
            Assert.Contains(surface == "composer" ? "Unsupported composer portrait fixture" : "Settled", disabledContent);
            if (surface == "node") Assert.Contains("<div class=\"label\">Confidence</div>", disabledContent);
        }
    }

    [Fact]
    public async Task DiscoveryControlsRenderUnansweredWithAccessibleRankingAndScaleEndpoints()
    {
        foreach (var format in new[] { "scale", "ranking" })
        {
            using var response = await _client.GetAsync($"/__test/question/{format}");
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            if (format == "scale")
            {
                Assert.Contains("id=\"numericAnswer\" value=\"\"", content);
                Assert.Contains("Minimum fixture", content);
                Assert.Contains("Maximum fixture", content);
                Assert.Contains("Unanswered", content);
            }
            else
            {
                Assert.Contains("id=\"confirmRanking\"", content);
                Assert.Contains("role=\"alert\" aria-live=\"polite\"", content);
                Assert.Contains("tabindex=\"0\"", content);
            }
            var fixtureDirectory = Environment.GetEnvironmentVariable("CU_PHASE1_BROWSER_FIXTURES");
            if (!string.IsNullOrEmpty(fixtureDirectory))
            {
                Directory.CreateDirectory(fixtureDirectory);
                await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, format + ".html"), content);
            }
        }
        using var reviewRequest = new HttpRequestMessage(HttpMethod.Get, "/Argument/Review/145");
        reviewRequest.Headers.Add("X-Test-User", "owner");
        using var reviewResponse = await _client.SendAsync(reviewRequest);
        var reviewContent = await reviewResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        Assert.Contains("type=\"button\" class=\"btn btn-outline-secondary btn-lg\" id=\"editBtn\"", reviewContent);
        var reviewDirectory = Environment.GetEnvironmentVariable("CU_PHASE1_BROWSER_FIXTURES");
        if (!string.IsNullOrEmpty(reviewDirectory))
            await File.WriteAllTextAsync(Path.Combine(reviewDirectory, "review.html"), reviewContent);
    }

    [Fact]
    public async Task SparseProfileAbstainsFromStoredPortraitWithRestorationWhenDisabled()
    {
        using var enabledResponse = await _client.GetAsync("/__test/profile");
        var enabledContent = await enabledResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        Assert.Contains("Insufficient information", enabledContent);
        Assert.Contains("Based on 0 responses", enabledContent);
        Assert.DoesNotContain("Unsupported sparse portrait fixture", enabledContent);
        Assert.DoesNotContain("99%", enabledContent);

        _host.Services.GetRequiredService<IConfiguration>()["AnalyticalContainment:Enabled"] = "false";
        using var disabledResponse = await _client.GetAsync("/__test/profile");
        var disabledContent = await disabledResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        Assert.Contains("Unsupported sparse portrait fixture", disabledContent);
        Assert.Contains("99%", disabledContent);
    }

    private sealed class FixtureDecomposition() : CommonUnderstanding.Services.ArgumentDecompositionService(
        null!, NullLogger<CommonUnderstanding.Services.ArgumentDecompositionService>.Instance)
    {
        public override Task<DecompositionResult> DecomposeAsync(string argumentText,
            Func<string, int, int, Task>? onProgress = null, Func<string, object?, Task>? onDebug = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DecompositionResult { ClaimText = "Synthetic claim", ClaimType = "empirical" });
    }

    private sealed class FixtureValidation() : CommonUnderstanding.Services.LogicalValidationService(
        null!, NullLogger<CommonUnderstanding.Services.LogicalValidationService>.Instance)
    {
        public override Task<CommonUnderstanding.Services.ValidationReport> ValidateAsync(DecompositionResult decomposition,
            string originalArgumentText, CancellationToken cancellationToken = default)
            => Task.FromResult(new CommonUnderstanding.Services.ValidationReport());
    }

    private sealed class FixtureAdjudication(TaskCompletionSource started, TaskCompletionSource release)
        : CommonUnderstanding.Services.AdjudicationEngine(null!, null!, null!,
            NullLogger<CommonUnderstanding.Services.AdjudicationEngine>.Instance)
    {
        public override async Task<AdjudicationSummary> AdjudicateAsync(int argumentId,
            Func<string, object?, Task>? onDebug = null, CancellationToken cancellationToken = default)
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return new AdjudicationSummary();
        }
    }

    private sealed class RegisteredControllerActivator : IControllerActivator
    {
        public object Create(ControllerContext context) => context.HttpContext.RequestServices
            .GetRequiredService(context.ActionDescriptor.ControllerTypeInfo.AsType());

        public void Release(ControllerContext context, object controller) { }
    }

    private sealed class ReportFixtureFeatureProvider
        : Microsoft.AspNetCore.Mvc.ApplicationParts.IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPart> parts,
            ControllerFeature feature) => feature.Controllers.Add(
                System.Reflection.IntrospectionExtensions.GetTypeInfo(typeof(ReportFixtureController)));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties)> _entries = [];
        public IReadOnlyList<(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties)> Entries => _entries;
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);
        public void Dispose() { }

        private sealed class CapturingLogger(
            List<(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                    ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                    : new Dictionary<string, object?>();
                entries.Add((logLevel, formatter(state, exception), properties));
            }
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userId = Request.Headers["X-Test-User"].ToString();
            if (string.IsNullOrWhiteSpace(userId))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, userId)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}