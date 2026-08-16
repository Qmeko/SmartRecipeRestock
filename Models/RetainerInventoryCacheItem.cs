namespace SmartRecipeRestockHelper.Models;

public sealed class RetainerInventoryCacheItem
{
    public ulong RetainerId { get; set; }
    public string RetainerName { get; set; } = string.Empty;
    public uint ItemId { get; set; }
    public bool HighQuality { get; set; }
    public uint Amount { get; set; }
    public string InventoryType { get; set; } = string.Empty;
    public int UiPage { get; set; }
    public int Slot { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
}
