using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace SmartRecipeRestockHelper.Services;

/// <summary>
/// Phase 4E-1c narrow executor for retainer item command.
/// This only sends RetrieveFromRetainer. It does not input quantity. The caller audits inventory difference.
/// </summary>
public sealed unsafe class RetainerItemCommandExecutor
{
    private const string RetainerItemCommandSignature =
        "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 48 8B 5C 24 ?? 41 8B F0";

    private readonly IGameGui _gameGui;
    private readonly IPluginLog _log;
    private readonly RetainerItemCommandDelegate? _retainerItemCommand;

    private delegate void RetainerItemCommandDelegate(
        nint agentRetainerItemCommandModule,
        uint slot,
        InventoryType inventoryType,
        uint a4,
        RetainerItemCommand command);

    private enum RetainerItemCommand : long
    {
        RetrieveFromRetainer = 0,
        EntrustToRetainer = 1,
        EntrustQuantity = 4,
        HaveRetainerSellItem = 5,
    }

    public RetainerItemCommandExecutor(ISigScanner sigScanner, IGameGui gameGui, IPluginLog log)
    {
        _gameGui = gameGui;
        _log = log;

        try
        {
            var address = sigScanner.ScanText(RetainerItemCommandSignature);
            if (address == nint.Zero)
            {
                _log.Warning("SmartRecipeRestockHelper: RetainerItemCommand signature returned zero.");
                return;
            }

            _retainerItemCommand = Marshal.GetDelegateForFunctionPointer<RetainerItemCommandDelegate>(address);
            _log.Information("SmartRecipeRestockHelper: RetainerItemCommand executor bound.");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "SmartRecipeRestockHelper: failed to bind RetainerItemCommand executor.");
            _retainerItemCommand = null;
        }
    }

    public bool IsBound => _retainerItemCommand != null;

    public bool IsInputNumericOpen()
    {
        var addon = _gameGui.GetAddonByName("InputNumeric");
        return addon.Address != nint.Zero;
    }

    public ExecuteRetainerItemCommandResult TrySendRetrieveCommand(InventoryType inventoryType, int slot)
    {
        if (_retainerItemCommand == null)
        {
            return ExecuteRetainerItemCommandResult.Blocked("RetainerItemCommand executor is not bound.");
        }

        if (slot < 0)
        {
            return ExecuteRetainerItemCommandResult.Blocked("Slot must be non-negative.");
        }

        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer);
        if (agent == null)
        {
            return ExecuteRetainerItemCommandResult.Blocked("AgentId.Retainer is unavailable.");
        }

        var agentRetainerItemCommandModule = (nint)agent + 40;
        if (agentRetainerItemCommandModule == nint.Zero)
        {
            return ExecuteRetainerItemCommandResult.Blocked("AgentRetainerItemCommandModule is zero.");
        }

        try
        {
            _retainerItemCommand(agentRetainerItemCommandModule, (uint)slot, inventoryType, 0, RetainerItemCommand.RetrieveFromRetainer);

            var inputNumericOpenImmediately = IsInputNumericOpen();

            return new ExecuteRetainerItemCommandResult
            {
                Success = true,
                CommandSent = true,
                InputNumericOpenImmediately = inputNumericOpenImmediately,
                InputNumericOpenAfterWait = inputNumericOpenImmediately,
                InputNumericProbeMs = 0,
                Message = $"RetrieveFromRetainer command sent. Container={inventoryType}; Slot={slot}; InputNumericOpenImmediately={inputNumericOpenImmediately}.",
            };
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "SmartRecipeRestockHelper: RetainerItemCommand execution failed.");
            return ExecuteRetainerItemCommandResult.Blocked("RetainerItemCommand execution failed: " + ex.Message);
        }
    }

    public sealed class ExecuteRetainerItemCommandResult
    {
        public bool Success { get; set; }

        public bool CommandSent { get; set; }

        public bool InputNumericOpenImmediately { get; set; }

        public bool InputNumericOpenAfterWait { get; set; }

        public int InputNumericProbeMs { get; set; }

        public string Message { get; set; } = string.Empty;

        public static ExecuteRetainerItemCommandResult Blocked(string message)
        {
            return new ExecuteRetainerItemCommandResult
            {
                Success = false,
                CommandSent = false,
                InputNumericOpenImmediately = false,
                Message = message,
            };
        }
    }
}
