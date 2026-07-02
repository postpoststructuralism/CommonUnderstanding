namespace CommonUnderstanding.Models.Widget.DTOs;

/// <summary>Request to register a new publisher site.</summary>
public record RegisterSiteRequest(
    string Domain,
    string SiteName,
    string PlanTier,
    string[]? AllowedOrigins,
    string? CustomCssUrl,
    string? LogoUrl
);

/// <summary>Response after registering a site (API key shown once).</summary>
public record RegisterSiteResponse(
    Guid SiteId,
    string ApiKey,
    string EmbedScriptUrl,
    string DashboardUrl
);

/// <summary>Request to create a new comment thread for a page.</summary>
public record CreateThreadRequest(
    string PageUrl,
    string? PageTitle
);

/// <summary>Request to post a comment to a thread.</summary>
public record PostCommentRequest(
    string Content,
    string? ParentArgumentId
);

/// <summary>Comment data returned by the API.</summary>
public record CommentDto(
    string Id,
    string AuthorName,
    string Content,
    int Upvotes,
    int Downvotes,
    int ReplyCount,
    double? WilsonScore,
    DateTime CreatedAt,
    string? ParentId,
    bool IsDeleted
);

/// <summary>Thread data returned by the API.</summary>
public record ThreadDto(
    string ThreadId,
    string PageUrl,
    string? PageTitle,
    int TotalComments,
    bool IsLocked,
    string SortOrder,
    List<CommentDto> Comments
);

/// <summary>Usage stats for the publisher dashboard.</summary>
public record UsageStatsDto(
    DateOnly Date,
    long PageViews,
    int CommentsPosted,
    int VotesCast,
    int AiAnalysesRun
);

/// <summary>Moderation queue item for the publisher dashboard.</summary>
public record ModerationQueueItemDto(
    string Id,
    string CommentId,
    string CommentSnippet,
    string Status,
    string? FlagReason,
    double? AiConfidence,
    DateTime CreatedAt
);

/// <summary>Cross-thread contradiction for the publisher dashboard.</summary>
public record ContradictionDto(
    string Id,
    string ThreadUrlA,
    string ThreadUrlB,
    string CommentSnippetA,
    string CommentSnippetB,
    string ContradictionType,
    double Confidence,
    string? Explanation,
    DateTime DetectedAt
);

/// <summary>Widget configuration for the embed script.</summary>
public record WidgetConfigDto(
    string SiteId,
    string ThreadId,
    string SortOrder,
    string? CustomCssUrl,
    string? LogoUrl,
    bool IsModerated,
    bool IsLocked
);