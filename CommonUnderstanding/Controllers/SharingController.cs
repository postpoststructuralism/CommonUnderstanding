using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

[Authorize]
public class SharingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserConnectionService _connectionService;
    private readonly UserProfileStore _profileStore;
    private readonly ILogger<SharingController> _logger;

    private static readonly JsonSerializerOptions _json = new();

    public SharingController(
        ApplicationDbContext db,
        UserConnectionService connectionService,
        UserProfileStore profileStore,
        ILogger<SharingController> logger)
    {
        _db = db;
        _connectionService = connectionService;
        _profileStore = profileStore;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Sharing/SharedWithMe
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> SharedWithMe()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        // Items where the user is in the SharedWithUserIds list OR visibility is Public/Connections (and user is connected)
        var connectedIds = (await _connectionService.GetConnectedUserIdsAsync(userId)).ToHashSet();

        var items = await _db.SharedItems
            .OrderByDescending(s => s.SharedAt)
            .ToListAsync();

        var visible = items.Where(item =>
        {
            if (item.Visibility == ItemVisibility.Public) return true;
            if (item.SharedByUserId == userId) return false; // own items shown in SharedByMe

            var targetIds = JsonSerializer.Deserialize<List<string>>(item.SharedWithUserIdsJson) ?? new();
            if (targetIds.Contains(userId)) return true;

            return item.Visibility == ItemVisibility.Connections && connectedIds.Contains(item.SharedByUserId);
        }).ToList();

        var vm = new SharedItemListViewModel
        {
            CurrentUserId = userId,
            Items = EnrichItems(visible)
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GET /Sharing/SharedByMe
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> SharedByMe()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return RedirectToAction("Start", "Discovery");

        var items = await _db.SharedItems
            .Where(s => s.SharedByUserId == userId)
            .OrderByDescending(s => s.SharedAt)
            .ToListAsync();

        var vm = new SharedItemListViewModel
        {
            CurrentUserId = userId,
            Items = EnrichItems(items)
        };

        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Sharing/Share
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Share(
        SharedItemType itemType,
        string itemReferenceId,
        string itemTitle,
        string? message,
        ItemVisibility visibility,
        string[]? shareWithUserIds)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        var item = new SharedItem
        {
            ItemType = itemType,
            ItemReferenceId = itemReferenceId,
            ItemTitle = itemTitle,
            SharedByUserId = userId,
            SharedWithUserIdsJson = JsonSerializer.Serialize(shareWithUserIds ?? Array.Empty<string>()),
            Visibility = visibility,
            Message = message
        };

        _db.SharedItems.Add(item);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {U} shared {T} {Id}", userId, itemType, itemReferenceId);
        TempData["Success"] = "Shared successfully.";

        return RedirectToAction(nameof(SharedByMe));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  POST /Sharing/React
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> React(int sharedItemId, string emoji, string? comment)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        var item = await _db.SharedItems.FindAsync(sharedItemId);
        if (item is null) return NotFound();

        var reactions = JsonSerializer.Deserialize<List<SharedItemReaction>>(item.ReactionsJson) ?? new();

        // Remove previous reaction by same user, then add new one
        reactions.RemoveAll(r => r.UserId == userId);
        reactions.Add(new SharedItemReaction
        {
            UserId = userId,
            Emoji = emoji,
            Comment = comment
        });

        item.ReactionsJson = JsonSerializer.Serialize(reactions);
        await _db.SaveChangesAsync();

        return Ok();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private List<SharedItemViewModel> EnrichItems(List<SharedItem> items)
    {
        return items.Select(item =>
        {
            var sharer = _profileStore.GetProfile(item.SharedByUserId);
            var reactions = JsonSerializer.Deserialize<List<SharedItemReaction>>(item.ReactionsJson) ?? new();
            return new SharedItemViewModel
            {
                Item = item,
                SharingUserName = sharer?.Name ?? item.SharedByUserId,
                Reactions = reactions
            };
        }).ToList();
    }
}

// ─────────────────────────────────────────────
//  View models
// ─────────────────────────────────────────────

public class SharedItemListViewModel
{
    public string CurrentUserId { get; set; } = string.Empty;
    public List<SharedItemViewModel> Items { get; set; } = new();
}

public class SharedItemViewModel
{
    public SharedItem Item { get; set; } = null!;
    public string SharingUserName { get; set; } = string.Empty;
    public List<SharedItemReaction> Reactions { get; set; } = new();
}
