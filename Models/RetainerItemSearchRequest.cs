namespace SmartRecipeRestockHelper.Models;

public sealed class RetainerItemSearchRequest
{
    public ulong RetainerId { get; set; }
    public uint ItemId { get; set; }
    public bool? Hq { get; set; }
    public int MaxAmount { get; set; }
}
