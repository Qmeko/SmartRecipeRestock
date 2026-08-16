namespace SmartRecipeRestockHelper.Models;

/// <summary>
/// Phase 4E-1n async command queue request.
/// RetrieveFromRetainer is queued and executed on the next Framework update,
/// so the IPC call can return before the UI/game command mutates state.
/// </summary>
public sealed class RetainerItemWithdrawRequest
{
    public ulong RetainerId { get; set; }

    public uint ItemId { get; set; }

    public bool? Hq { get; set; }

    /// <summary>
    /// Requested amount kept for logging. RetrieveFromRetainer retrieves the full stack.
    /// </summary>
    public int Amount { get; set; } = 1;

    public int? InventorySlot { get; set; }

    public string? InventoryContainer { get; set; }

    public bool DryRun { get; set; } = true;

    public bool AllowActualWithdraw { get; set; } = false;

    public bool ConfirmActualWithdraw { get; set; } = false;

    public bool OneItemTestOnly { get; set; } = false;

    public bool RequireExactSlot { get; set; } = true;

    public bool EnableRetrieveCommand { get; set; } = false;

    public bool NoQuantityInput { get; set; } = true;

    public bool AllowFullStackWithdraw { get; set; } = false;

    public bool ExpectInputNumeric { get; set; } = false;

    public bool RequireRetainerUiPageMatch { get; set; } = false;

    public int? CurrentRetainerUiPage { get; set; }

    public bool AssumeWithdrawOnCommandSent { get; set; } = false;

    public bool NoBlockingAudit { get; set; } = true;

    /// <summary>
    /// If true, do not call RetrieveFromRetainer directly inside the IPC call.
    /// Queue it for the next Framework update and return success immediately.
    /// </summary>
    public bool QueueCommandOnFrameworkUpdate { get; set; } = true;

    /// <summary>
    /// Delay before executing the queued command. This lets SND finish the macro command
    /// before the helper mutates the retainer inventory UI.
    /// </summary>
    public int QueueCommandDelayMs { get; set; } = 750;

    public bool AssumeWithdrawOnIpcError { get; set; } = false;

    public int PostCommandAuditAttempts { get; set; } = 0;

    public int PostCommandAuditIntervalMs { get; set; } = 0;

    public int PostCommandAuditDelayMs { get; set; } = 0;
}
