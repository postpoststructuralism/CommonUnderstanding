using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers;

/// <summary>
/// MVC controller that serves Razor views for Phase 2 social features.
/// API calls from the views go directly to the Social API controllers.
/// </summary>
public class SocialViewController : Controller
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public SocialViewController(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // GET /Social/Feed
    public IActionResult Feed()
    {
        ViewData["Title"] = "Social Feed";
        return View("~/Views/Social/Feed.cshtml");
    }

    // GET /Social/ChainBuilder
    public IActionResult ChainBuilder()
    {
        ViewData["Title"] = "Chain Builder";
        return View("~/Views/Social/ChainBuilder.cshtml");
    }

    // GET /Social/WorldviewComposer
    public IActionResult WorldviewComposer()
    {
        ViewData["Title"] = "Worldview Composer";
        return View("~/Views/Social/WorldviewComposer.cshtml");
    }

    // GET /Social/DebateRoom/{id?}
    public async Task<IActionResult> DebateRoom(Guid? id, CancellationToken ct = default)
    {
        ViewData["Title"] = "Debate Room";

        if (id.HasValue)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var room = await db.DebateRooms
                .AsNoTracking()
                .Include(r => r.Contributions)
                    .ThenInclude(c => c.Argument)
                .FirstOrDefaultAsync(r => r.Id == id.Value, ct);

            if (room is not null)
                ViewBag.Room = room;
        }

        return View("~/Views/Social/DebateRoom.cshtml");
    }

    // GET /Social/Detail/{id}
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var arg = await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Include(a => a.Votes)
            .Include(a => a.OutboundLinks)
                .ThenInclude(l => l.TargetArgument)
                    .ThenInclude(a => a!.ClaimProposition)
            .Include(a => a.InboundLinks)
                .ThenInclude(l => l.SourceArgument)
                    .ThenInclude(a => a!.ClaimProposition)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (arg is null || (!arg.IsPublic && arg.UserId != userId))
            return NotFound();

        ViewData["Title"] = arg.Title;
        ViewBag.UserVote = arg.Votes.FirstOrDefault(v => v.UserId == userId);
        return View("~/Views/Social/Detail.cshtml", arg);
    }
}
