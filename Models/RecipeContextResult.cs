namespace SmartRecipeRestockHelper.Models;

public sealed class RecipeContextResult
{
    public bool Success { get; set; }
    public uint? RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public uint? ResultItemId { get; set; }
    public string ResultItemName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsRecipeUiOpen { get; set; }
    public string DetectedAddonName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
