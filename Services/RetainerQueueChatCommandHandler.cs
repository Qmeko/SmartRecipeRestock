using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

/// <summary>
/// /srstockqueue command route.
/// This avoids SND IPC for the actual retrieval path.
/// SND only sends a normal Dalamud chat command, then the helper scans and queues the command.
/// </summary>
public sealed class RetainerQueueChatCommandHandler : IDisposable
{
    public const string CommandName = "/srstockqueue";

    private readonly ICommandManager _commandManager;
    private readonly RetainerInventoryInspector _inspector;
    private readonly RetainerWithdrawCommandQueue _queue;
    private readonly IPluginLog _log;

    public RetainerQueueChatCommandHandler(
        ICommandManager commandManager,
        RetainerInventoryInspector inspector,
        RetainerWithdrawCommandQueue queue,
        IPluginLog log)
    {
        _commandManager = commandManager;
        _inspector = inspector;
        _queue = queue;
        _log = log;

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Queue SmartRecipeRestock retainer retrieve: /srstockqueue <retainerId> <itemId> <hq:true|false> <currentPage> <delayMs>",
            ShowInHelp = false,
        });
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        try
        {
            var parsed = ParseArgs(args);
            if (parsed == null)
            {
                _log.Warning(
                    "SmartRecipeRestockHelper: {Command} invalid args. Args={Args}",
                    CommandName,
                    args);
                return;
            }

            var search = _inspector.FindRetainerItem(new RetainerItemSearchRequest
            {
                RetainerId = parsed.RetainerId,
                ItemId = parsed.ItemId,
                Hq = parsed.Hq,
                MaxAmount = 0,
            });

            if (!search.Success || !search.CanIdentifyRow || search.InventorySlot == null)
            {
                _log.Warning(
                    "SmartRecipeRestockHelper: {Command} blocked. Target row not identified. ItemId={ItemId}; Message={Message}",
                    CommandName,
                    parsed.ItemId,
                    search.Message);
                return;
            }

            if (!parsed.CurrentPage.HasValue)
            {
                _log.Warning(
                    "SmartRecipeRestockHelper: {Command} blocked. Current page is required.",
                    CommandName);
                return;
            }

            if (!search.RetainerUiPageCandidate.HasValue)
            {
                _log.Warning(
                    "SmartRecipeRestockHelper: {Command} blocked. Target page candidate is unknown. Container={Container}; Slot={Slot}",
                    CommandName,
                    search.InventoryContainer,
                    search.InventorySlot);
                return;
            }

            if (parsed.CurrentPage.Value != search.RetainerUiPageCandidate.Value)
            {
                _log.Warning(
                    "SmartRecipeRestockHelper: {Command} blocked. Page mismatch. CurrentPage={CurrentPage}; TargetPage={TargetPage}; Container={Container}; Slot={Slot}",
                    CommandName,
                    parsed.CurrentPage.Value,
                    search.RetainerUiPageCandidate.Value,
                    search.InventoryContainer,
                    search.InventorySlot);
                return;
            }

            if (!RetainerInventoryInspector.TryParseInventoryType(search.InventoryContainer, out var inventoryType))
            {
                _log.Warning(
                    "SmartRecipeRestockHelper: {Command} blocked. Cannot parse InventoryContainer={Container}",
                    CommandName,
                    search.InventoryContainer ?? "nil");
                return;
            }

            if (!_queue.QueueRetrieveCommand(inventoryType, search.InventorySlot.Value, parsed.DelayMs, out var queueMessage))
            {
                _log.Warning(
                    "SmartRecipeRestockHelper: {Command} queue failed. {Message}",
                    CommandName,
                    queueMessage);
                return;
            }

            _log.Information(
                "SmartRecipeRestockHelper: {Command} queued RetrieveFromRetainer. ItemId={ItemId}; Container={Container}; Slot={Slot}; Amount={Amount}; UiPage={UiPage}; DelayMs={DelayMs}; Message={Message}",
                CommandName,
                parsed.ItemId,
                search.InventoryContainer,
                search.InventorySlot.Value,
                search.AvailableAmount,
                search.RetainerUiPageCandidate.Value,
                parsed.DelayMs,
                queueMessage);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "SmartRecipeRestockHelper: /srstockqueue threw.");
        }
    }

    private static ParsedArgs? ParseArgs(string args)
    {
        var parts = args
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 5)
        {
            return null;
        }

        if (!ulong.TryParse(parts[0], out var retainerId))
        {
            return null;
        }

        if (!uint.TryParse(parts[1], out var itemId))
        {
            return null;
        }

        bool? hq = parts[2].Equals("nil", StringComparison.OrdinalIgnoreCase)
            ? null
            : bool.TryParse(parts[2], out var hqValue)
                ? hqValue
                : null;

        if (!int.TryParse(parts[3], out var currentPage))
        {
            return null;
        }

        if (!int.TryParse(parts[4], out var delayMs))
        {
            return null;
        }

        delayMs = Math.Clamp(delayMs, 250, 5000);

        return new ParsedArgs(retainerId, itemId, hq, currentPage, delayMs);
    }

    private sealed record ParsedArgs(
        ulong RetainerId,
        uint ItemId,
        bool? Hq,
        int? CurrentPage,
        int DelayMs);
}
