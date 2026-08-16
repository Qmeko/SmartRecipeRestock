namespace SmartRecipeRestockHelper.Models;

public sealed class SmartRestockStatus
{
    public string Phase { get; set; } = "4A";
    public string Version { get; set; } = string.Empty;
    public bool ReadOnly { get; set; } = true;
    public bool WithdrawalEnabled { get; set; } = false;
    public bool ItemSelectionEnabled { get; set; } = false;
    public string DetectedAddonName { get; set; } = string.Empty;
    public bool TransferScreenOpen { get; set; }
    public bool RetainerContextValid { get; set; }
    public string Message { get; set; } = string.Empty;
}
