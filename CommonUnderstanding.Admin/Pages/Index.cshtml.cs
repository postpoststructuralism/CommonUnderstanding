using CommonUnderstanding.Admin.Models;
using CommonUnderstanding.Admin.Options;
using CommonUnderstanding.Admin.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CommonUnderstanding.Admin.Pages;

public sealed class IndexModel(DashboardService dashboardService, IOptions<DashboardOptions> options) : PageModel
{
    private static readonly int[] AllowedRanges = [6, 24, 168, 720];

    [BindProperty(SupportsGet = true)]
    public int Hours { get; set; }

    public DashboardSnapshot Snapshot { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!AllowedRanges.Contains(Hours))
        {
            Hours = AllowedRanges.Contains(options.Value.DefaultRangeHours) ? options.Value.DefaultRangeHours : 24;
        }

        Snapshot = await dashboardService.GetAsync(Hours, cancellationToken);
    }
}