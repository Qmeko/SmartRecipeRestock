namespace SmartRecipeRestockHelper.Models;

/// <summary>Stable JSON shape returned by GetStatus / ValidateRetainerContext IPC.</summary>
public sealed class SmartRestockStatusJson
{
    public bool ReadOnly { get; set; } = true;
    public bool WithdrawalEnabled { get; set; }
    public bool TransferScreenOpen { get; set; }
    public string DetectedAddonName { get; set; } = string.Empty;
}
