namespace SmartRecipeRestockHelper.Models;

public sealed class RestockMaterialRow
{
    public uint ItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsCrystal { get; init; }

    public int AmountPerCraft { get; init; }

    public int Needed { get; init; }

    public int PlayerCount { get; init; }

    public int RetainerCount { get; init; }

    public int Missing { get; init; }

    public bool CanWithdrawNow { get; init; }

    public string SourceRetainers { get; init; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
