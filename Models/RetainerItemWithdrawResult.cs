namespace SmartRecipeRestockHelper.Models;

public sealed class RetainerItemWithdrawResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Phase { get; set; } = "4E1n-ChatCommandQueue";

    public ulong RetainerId { get; set; }

    public uint ItemId { get; set; }

    public bool? Hq { get; set; }

    public int RequestedAmount { get; set; }

    public int AvailableAmount { get; set; }

    public int BeforeAmount { get; set; }

    public int AfterAmount { get; set; }

    public int WithdrawnAmount { get; set; }

    public int PlannedAmount { get; set; }

    public int? InventorySlot { get; set; }

    public string? InventoryContainer { get; set; }

    public int? RetainerUiPageCandidate { get; set; }

    public int? CurrentRetainerUiPage { get; set; }

    public bool RetainerUiPageMatched { get; set; }

    public string DetectedAddonName { get; set; } = string.Empty;

    public bool CanIdentifyRow { get; set; }

    public bool CanWithdraw { get; set; }

    public bool DidWithdraw { get; set; }

    public bool DryRun { get; set; } = true;

    public bool SafetyGatePassed { get; set; }

    public bool ExecutorBound { get; set; }

    /// <summary>
    /// True when the command has been queued to execute on the next framework update.
    /// </summary>
    public bool CommandQueued { get; set; }

    /// <summary>
    /// For compatibility with older Lua log parsing, this is true when CommandQueued=true.
    /// </summary>
    public bool CommandSent { get; set; }

    public int QueueCommandDelayMs { get; set; }

    public bool InputNumericExpected { get; set; }

    public bool InputNumericOpenImmediately { get; set; }

    public bool InputNumericOpenAfterWait { get; set; }

    public bool StockChangedAfterCommand { get; set; }

    public bool AuditConfirmed { get; set; }

    public bool AssumedWithdrawOnCommandSent { get; set; }

    public bool NoBlockingAudit { get; set; }

    public int AuditAttempts { get; set; }

    public int AuditWaitTotalMs { get; set; }

    public int PostCommandAuditDelayMs { get; set; }

    public string Reason { get; set; } = string.Empty;
}
