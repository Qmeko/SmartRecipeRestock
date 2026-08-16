using FFXIVClientStructs.FFXIV.Client.Game;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

/// <summary>
/// Phase 4E-1n guarded full-stack retrieval.
/// The actual RetrieveFromRetainer command is queued to the next Framework update.
/// IPC returns before the UI/game state mutates, avoiding SND macro failure.
/// </summary>
public sealed class RetainerItemWithdrawer
{
    private readonly RetainerInventoryInspector _inspector;
    private readonly RetainerItemCommandExecutor? _commandExecutor;
    private readonly RetainerWithdrawCommandQueue? _commandQueue;

    public RetainerItemWithdrawer(
        RetainerInventoryInspector inspector,
        RetainerItemCommandExecutor? commandExecutor = null,
        RetainerWithdrawCommandQueue? commandQueue = null)
    {
        _inspector = inspector;
        _commandExecutor = commandExecutor;
        _commandQueue = commandQueue;
    }

    public RetainerItemWithdrawResult PreviewWithdraw(RetainerItemWithdrawRequest request)
    {
        var before = FindCurrentTarget(request);
        var result = BuildBaseResult(request);
        ApplyBeforeSearch(result, before);

        if (!before.Success || !before.CanIdentifyRow || before.InventorySlot == null)
        {
            result.Success = false;
            result.Message = "PHASE4E1N_PREVIEW_BLOCKED Target row is not safely identified.";
            result.Reason = before.Message;
            return result;
        }

        var validationError = ValidateCommonGates(request, before, requireActualGates: false);
        if (validationError != null)
        {
            result.Success = false;
            result.Message = "PHASE4E1N_PREVIEW_BLOCKED " + validationError;
            result.Reason = validationError;
            return result;
        }

        result.PlannedAmount = before.AvailableAmount;
        result.Success = true;
        result.SafetyGatePassed = true;
        result.ExecutorBound = _commandExecutor?.IsBound == true;
        result.Message =
            $"PHASE4E1N_PREVIEW_OK Full-stack retrieval preview only. UiPageCandidate={before.RetainerUiPageCandidate}; Slot={before.InventorySlot.Value}; PlannedAmount={result.PlannedAmount}; No item was withdrawn.";
        result.Reason = "Preview succeeded. RetrieveFromRetainer is treated as full-stack retrieval.";
        return result;
    }

    public RetainerItemWithdrawResult Withdraw(RetainerItemWithdrawRequest request)
    {
        var before = FindCurrentTarget(request);
        var result = BuildBaseResult(request);
        ApplyBeforeSearch(result, before);

        if (!before.Success || !before.CanIdentifyRow || before.InventorySlot == null)
        {
            result.Success = false;
            result.Message = "PHASE4E1N_WITHDRAW_BLOCKED Target row is not safely identified.";
            result.Reason = before.Message;
            return result;
        }

        var validationError = ValidateCommonGates(request, before, requireActualGates: true);
        if (validationError != null)
        {
            result.Success = false;
            result.Message = "PHASE4E1N_WITHDRAW_BLOCKED " + validationError;
            result.Reason = validationError;
            return result;
        }

        if (!request.EnableRetrieveCommand)
        {
            result.Success = false;
            result.Message = "PHASE4E1N_WITHDRAW_BLOCKED EnableRetrieveCommand must be true.";
            result.Reason = "The real retainer item command is behind an extra explicit gate.";
            return result;
        }

        if (!request.NoQuantityInput)
        {
            result.Success = false;
            result.Message = "PHASE4E1N_WITHDRAW_BLOCKED This package does not input quantity.";
            result.Reason = "Quantity input is not used because RetrieveFromRetainer retrieves the full stack.";
            return result;
        }

        if (!request.AllowFullStackWithdraw)
        {
            result.Success = false;
            result.Message = "PHASE4E1N_WITHDRAW_BLOCKED AllowFullStackWithdraw must be true.";
            result.Reason = "RetrieveFromRetainer retrieves the full stack.";
            return result;
        }

        if (_commandExecutor == null || !_commandExecutor.IsBound)
        {
            result.ExecutorBound = false;
            result.Success = false;
            result.Message = "PHASE4E1N_WITHDRAW_BLOCKED RetainerItemCommand executor is not bound.";
            result.Reason = "Signature binding failed or executor was not injected.";
            return result;
        }

        if (!RetainerInventoryInspector.TryParseInventoryType(before.InventoryContainer, out var inventoryType))
        {
            result.Success = false;
            result.Message = "PHASE4E1N_WITHDRAW_BLOCKED InventoryContainer could not be parsed.";
            result.Reason = "InventoryContainer=" + (before.InventoryContainer ?? "nil");
            return result;
        }

        result.SafetyGatePassed = true;
        result.ExecutorBound = true;
        result.PlannedAmount = before.AvailableAmount;
        result.CanWithdraw = false;

        if (request.QueueCommandOnFrameworkUpdate)
        {
            if (_commandQueue == null)
            {
                result.Success = false;
                result.Message = "PHASE4E1N_QUEUE_BLOCKED Command queue is not available.";
                result.Reason = "RetainerWithdrawCommandQueue was not injected.";
                return result;
            }

            if (!_commandQueue.QueueRetrieveCommand(inventoryType, before.InventorySlot.Value, request.QueueCommandDelayMs, out var queueMessage))
            {
                result.Success = false;
                result.Message = "PHASE4E1N_QUEUE_BLOCKED " + queueMessage;
                result.Reason = queueMessage;
                return result;
            }

            result.Success = true;
            result.DidWithdraw = true;
            result.AuditConfirmed = false;
            result.AssumedWithdrawOnCommandSent = true;
            result.NoBlockingAudit = true;
            result.CommandQueued = true;
            result.CommandSent = true;
            result.WithdrawnAmount = result.PlannedAmount;
            result.AfterAmount = result.BeforeAmount;
            result.Message =
                $"PHASE4E1N_WITHDRAW_QUEUED RetrieveFromRetainer command queued for delayed Framework update. DelayMs={request.QueueCommandDelayMs}; Before={result.BeforeAmount}; AssumedWithdrawnAmount={result.WithdrawnAmount}; DidWithdraw=true; AuditConfirmed=false.";
            result.Reason = queueMessage;
            return result;
        }

        // Legacy direct mode remains available, but should not be used with SND unless explicitly testing.
        var exec = _commandExecutor.TrySendRetrieveCommand(inventoryType, before.InventorySlot.Value);
        result.CommandSent = exec.CommandSent;
        result.CommandQueued = false;
        result.InputNumericExpected = false;
        result.InputNumericOpenImmediately = exec.InputNumericOpenImmediately;
        result.InputNumericOpenAfterWait = exec.InputNumericOpenAfterWait;
        result.DryRun = false;

        if (!exec.Success)
        {
            result.Success = false;
            result.Message = "PHASE4E1N_COMMAND_BLOCKED " + exec.Message;
            result.Reason = exec.Message;
            return result;
        }

        result.Success = true;
        result.DidWithdraw = true;
        result.AuditConfirmed = false;
        result.AssumedWithdrawOnCommandSent = true;
        result.NoBlockingAudit = true;
        result.WithdrawnAmount = result.PlannedAmount;
        result.AfterAmount = result.BeforeAmount;
        result.Message =
            $"PHASE4E1N_WITHDRAW_ASSUMED_DIRECT RetrieveFromRetainer command was sent directly. Before={result.BeforeAmount}; AssumedWithdrawnAmount={result.WithdrawnAmount}; DidWithdraw=true; AuditConfirmed=false.";
        result.Reason = "Direct command mode.";
        return result;
    }

    private RetainerItemSearchResult FindCurrentTarget(RetainerItemWithdrawRequest request)
    {
        return _inspector.FindRetainerItem(new RetainerItemSearchRequest
        {
            RetainerId = request.RetainerId,
            ItemId = request.ItemId,
            Hq = request.Hq,
            MaxAmount = 0,
        });
    }

    private static RetainerItemWithdrawResult BuildBaseResult(RetainerItemWithdrawRequest request)
    {
        return new RetainerItemWithdrawResult
        {
            Success = false,
            Phase = "4E1n-ChatCommandQueue",
            RetainerId = request.RetainerId,
            ItemId = request.ItemId,
            Hq = request.Hq,
            RequestedAmount = request.Amount,
            AvailableAmount = 0,
            BeforeAmount = 0,
            AfterAmount = 0,
            WithdrawnAmount = 0,
            PlannedAmount = 0,
            InventorySlot = request.InventorySlot,
            InventoryContainer = request.InventoryContainer,
            CurrentRetainerUiPage = request.CurrentRetainerUiPage,
            RetainerUiPageMatched = false,
            CanIdentifyRow = false,
            CanWithdraw = false,
            DidWithdraw = false,
            DryRun = request.DryRun,
            SafetyGatePassed = false,
            ExecutorBound = false,
            CommandQueued = false,
            CommandSent = false,
            InputNumericExpected = false,
            InputNumericOpenImmediately = false,
            InputNumericOpenAfterWait = false,
            StockChangedAfterCommand = false,
            AuditConfirmed = false,
            AssumedWithdrawOnCommandSent = false,
            NoBlockingAudit = request.NoBlockingAudit,
            AuditAttempts = 0,
            AuditWaitTotalMs = 0,
            QueueCommandDelayMs = request.QueueCommandDelayMs,
            PostCommandAuditDelayMs = request.PostCommandAuditDelayMs,
        };
    }

    private static void ApplyBeforeSearch(RetainerItemWithdrawResult result, RetainerItemSearchResult search)
    {
        result.AvailableAmount = search.AvailableAmount;
        result.BeforeAmount = search.AvailableAmount;
        result.InventorySlot = search.InventorySlot;
        result.InventoryContainer = search.InventoryContainer;
        result.RetainerUiPageCandidate = search.RetainerUiPageCandidate;
        result.RetainerUiPageMatched =
            result.CurrentRetainerUiPage.HasValue
            && search.RetainerUiPageCandidate.HasValue
            && result.CurrentRetainerUiPage.Value == search.RetainerUiPageCandidate.Value;
        result.DetectedAddonName = search.DetectedAddonName;
        result.CanIdentifyRow = search.CanIdentifyRow;
    }

    private static string? ValidateCommonGates(
        RetainerItemWithdrawRequest request,
        RetainerItemSearchResult search,
        bool requireActualGates)
    {
        if (request.ItemId == 0)
        {
            return "ItemId must be non-zero.";
        }

        if (request.Amount <= 0)
        {
            return "Amount must be positive.";
        }

        if (search.AvailableAmount <= 0)
        {
            return "No available amount.";
        }

        if (request.RequireExactSlot && request.InventorySlot.HasValue && search.InventorySlot.HasValue)
        {
            if (request.InventorySlot.Value != search.InventorySlot.Value)
            {
                return $"Requested slot does not match current scanned slot. RequestedSlot={request.InventorySlot.Value}; CurrentSlot={search.InventorySlot.Value}.";
            }
        }

        if (request.RequireExactSlot && !request.InventorySlot.HasValue)
        {
            return "RequireExactSlot=true but request.InventorySlot is null.";
        }

        if (request.RequireRetainerUiPageMatch)
        {
            if (!request.CurrentRetainerUiPage.HasValue)
            {
                return "RequireRetainerUiPageMatch=true but CurrentRetainerUiPage is null.";
            }

            if (!search.RetainerUiPageCandidate.HasValue)
            {
                return "RequireRetainerUiPageMatch=true but target RetainerUiPageCandidate is unknown.";
            }

            if (request.CurrentRetainerUiPage.Value != search.RetainerUiPageCandidate.Value)
            {
                return $"Target appears to be on retainer UI page {search.RetainerUiPageCandidate.Value}, but CurrentRetainerUiPage={request.CurrentRetainerUiPage.Value}. Page switching is not automated yet.";
            }
        }

        if (!requireActualGates)
        {
            return null;
        }

        if (request.DryRun)
        {
            return "DryRun must be false for actual withdrawal.";
        }

        if (!request.AllowActualWithdraw)
        {
            return "AllowActualWithdraw must be true for actual withdrawal.";
        }

        if (!request.ConfirmActualWithdraw)
        {
            return "ConfirmActualWithdraw must be true for actual withdrawal.";
        }

        return null;
    }
}
