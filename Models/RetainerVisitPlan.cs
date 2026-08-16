namespace SmartRecipeRestockHelper.Models;

public sealed class RetainerVisitPlan
{
    public ulong RetainerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int ListIndex { get; init; }

    public List<uint> ItemIds { get; init; } = [];

    public string ItemSummary { get; init; } = string.Empty;

    public string Status { get; set; } = "予定";
}
