using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Widget;

/// <summary>
/// Daily usage tracking for billing and analytics per publisher site.
/// </summary>
public class WidgetUsage
{
    public Guid Id { get; set; }

    [Required]
    public Guid SiteId { get; set; }

    public DateOnly Date { get; set; }

    public long PageViews { get; set; } = 0;
    public int CommentsPosted { get; set; } = 0;
    public int VotesCast { get; set; } = 0;
    public int AiAnalysesRun { get; set; } = 0;
    public long BandwidthBytes { get; set; } = 0;
}