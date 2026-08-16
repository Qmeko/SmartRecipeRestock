namespace SmartRecipeRestockHelper.Models;

public sealed class RecipeMaterial
{
    public uint ItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int AmountPerCraft { get; init; }

    public bool IsCrystal { get; init; }
}
