using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace SmartRecipeRestockHelper.Services;

/// <summary>
/// Runs the actual RetrieveFromRetainer command after a delay on Framework updates,
/// after the IPC call and SND macro command have returned.
/// </summary>
public sealed class RetainerWithdrawCommandQueue : IDisposable
{
    private readonly IFramework _framework;
    private readonly RetainerItemCommandExecutor _executor;
    private readonly IPluginLog _log;
    private readonly object _lock = new();

    private readonly Queue<PendingCommand> _pending = new();
    private PendingCommand? _active;
    private bool _subscribed;

    public bool HasPending
    {
        get
        {
            lock (_lock)
            {
                return _active != null || _pending.Count > 0;
            }
        }
    }

    public RetainerWithdrawCommandQueue(
        IFramework framework,
        RetainerItemCommandExecutor executor,
        IPluginLog log)
    {
        _framework = framework;
        _executor = executor;
        _log = log;
    }

    public bool QueueRetrieveCommand(InventoryType inventoryType, int slot, int delayMs, out string message)
    {
        if (!_executor.IsBound)
        {
            message = "RetainerItemCommand executor is not bound.";
            return false;
        }

        if (slot < 0)
        {
            message = "Slot must be non-negative.";
            return false;
        }

        delayMs = Math.Clamp(delayMs, 250, 5000);

        lock (_lock)
        {
            _pending.Enqueue(new PendingCommand(
                inventoryType,
                slot,
                Environment.TickCount64,
                delayMs));

            if (!_subscribed)
            {
                _framework.Update += OnFrameworkUpdate;
                _subscribed = true;
            }
        }

        message = $"RetrieveFromRetainer command queued. Container={inventoryType}; Slot={slot}; DelayMs={delayMs}.";
        return true;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_subscribed)
            {
                _framework.Update -= OnFrameworkUpdate;
                _subscribed = false;
            }

            _pending.Clear();
            _active = null;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        PendingCommand? command;

        lock (_lock)
        {
            if (_active == null)
            {
                if (_pending.Count == 0)
                {
                    if (_subscribed)
                    {
                        _framework.Update -= OnFrameworkUpdate;
                        _subscribed = false;
                    }

                    return;
                }

                var next = _pending.Dequeue();
                _active = next with { EnqueuedTickMs = Environment.TickCount64 };
            }

            command = _active;
            var elapsedMs = Environment.TickCount64 - command.EnqueuedTickMs;
            if (elapsedMs < command.DelayMs)
            {
                return;
            }

            _active = null;

            if (_pending.Count == 0 && _subscribed)
            {
                _framework.Update -= OnFrameworkUpdate;
                _subscribed = false;
            }
        }

        try
        {
            var result = _executor.TrySendRetrieveCommand(command.InventoryType, command.Slot);
            if (result.Success)
            {
                _log.Information(
                    "SmartRecipeRestockHelper: delayed async RetrieveFromRetainer executed. Container={InventoryType}; Slot={Slot}; DelayMs={DelayMs}; CommandSent={CommandSent}; Message={Message}",
                    command.InventoryType,
                    command.Slot,
                    command.DelayMs,
                    result.CommandSent,
                    result.Message);
            }
            else
            {
                _log.Warning(
                    "SmartRecipeRestockHelper: delayed async RetrieveFromRetainer failed. Container={InventoryType}; Slot={Slot}; DelayMs={DelayMs}; Message={Message}",
                    command.InventoryType,
                    command.Slot,
                    command.DelayMs,
                    result.Message);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "SmartRecipeRestockHelper: delayed async RetrieveFromRetainer threw.");
        }
    }

    private sealed record PendingCommand(
        InventoryType InventoryType,
        int Slot,
        long EnqueuedTickMs,
        int DelayMs);
}
