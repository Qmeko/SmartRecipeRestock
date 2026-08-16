namespace SmartRecipeRestockHelper.Models;

public sealed class RetainerItemSearchResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public ulong RetainerId { get; set; }

    public uint ItemId { get; set; }

    public bool? Hq { get; set; }

    public int AvailableAmount { get; set; }

    public int? InventorySlot { get; set; }

    public string? InventoryContainer { get; set; }

    /// <summary>
    /// Heuristic UI page candidate for FFXIV retainer inventory page display.
    /// This is diagnostic/guard data, not a confirmed page switch API.
    /// </summary>
    public int? RetainerUiPageCandidate { get; set; }

    public int MatchedSlots { get; set; }

    public string DetectedAddonName { get; set; } = string.Empty;

    public bool CanIdentifyRow { get; set; }

    public bool CanWithdraw { get; set; }

    public string Reason { get; set; } = string.Empty;
}
