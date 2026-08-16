namespace SmartRecipeRestockHelper.Models;

public sealed class RetainerInventoryCacheFile
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTimeOffset UpdatedAt { get; set; }
    public List<RetainerInventoryCacheItem> Items { get; set; } = new();
}
