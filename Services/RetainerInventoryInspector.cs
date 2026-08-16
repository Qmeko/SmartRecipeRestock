using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

/// <summary>
/// Phase 4B/4E helper-side inventory reader.
/// Read-only: scans retainer inventory containers and identifies the matching slot.
/// Does not invoke callbacks, select rows, open menus, input quantities, or withdraw items.
/// </summary>
public sealed unsafe class RetainerInventoryInspector
{
    private static readonly InventoryType[] RetainerInventoryTypes =
    [
        InventoryType.RetainerPage1,
        InventoryType.RetainerPage2,
        InventoryType.RetainerPage3,
        InventoryType.RetainerPage4,
        InventoryType.RetainerPage5,
        InventoryType.RetainerPage6,
        InventoryType.RetainerPage7,
        InventoryType.RetainerCrystals,
    ];

    private static readonly string[] TransferAddonNames =
    [
        "Inventory",
        "InventoryRetainer",
        "InventoryRetainerLarge",
    ];

    private readonly IGameGui _gameGui;
    private readonly ICondition _condition;

    public RetainerInventoryInspector(IGameGui gameGui, ICondition condition)
    {
        _gameGui = gameGui;
        _condition = condition;
    }

    public bool IsAtRetainerBell => _condition[ConditionFlag.OccupiedSummoningBell];

    public bool IsTransferOpen => DetectTransferAddonName() != null;

    public string? DetectTransferAddonName()
    {
        foreach (var name in TransferAddonNames)
        {
            var addon = _gameGui.GetAddonByName(name);
            if (addon.Address != nint.Zero)
            {
                return name;
            }
        }

        return null;
    }

    public int CountRetainerStock(uint itemId)
    {
        if (itemId == 0 || !IsAtRetainerBell)
        {
            return 0;
        }

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            return 0;
        }

        var total = 0;
        foreach (var containerType in RetainerInventoryTypes)
        {
            var container = inventoryManager->GetInventoryContainer(containerType);
            if (container == null)
            {
                continue;
            }

            for (var slotIndex = 0; slotIndex < container->Size; slotIndex++)
            {
                var slot = container->GetInventorySlot(slotIndex);
                if (slot == null || slot->ItemId != itemId || slot->Quantity <= 0)
                {
                    continue;
                }

                total += slot->Quantity;
            }
        }

        return total;
    }

    public RetainerItemSearchResult FindRetainerItem(RetainerItemSearchRequest request)
    {
        var detectedAddon = DetectTransferAddonName() ?? string.Empty;
        var result = new RetainerItemSearchResult
        {
            RetainerId = request.RetainerId,
            ItemId = request.ItemId,
            Hq = request.Hq,
            DetectedAddonName = detectedAddon,
            CanWithdraw = false,
            Success = false,
            CanIdentifyRow = false,
            InventorySlot = null,
            InventoryContainer = null,
            AvailableAmount = 0,
            MatchedSlots = 0,
        };

        if (request.ItemId == 0)
        {
            result.Message = "PHASE4E1D_PAGE_GUARD_SCAN ItemId must be non-zero.";
            return result;
        }

        if (!_condition[ConditionFlag.OccupiedSummoningBell])
        {
            result.Message = "PHASE4E1D_PAGE_GUARD_SCAN Not at retainer bell (OccupiedSummoningBell=false).";
            return result;
        }

        if (string.IsNullOrEmpty(detectedAddon))
        {
            result.Message = "PHASE4E1D_PAGE_GUARD_SCAN Item transfer addon not detected. Open transfer screen first.";
            return result;
        }

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            result.Message = "PHASE4E1D_PAGE_GUARD_SCAN InventoryManager unavailable.";
            return result;
        }

        var totalAmount = 0;
        var firstSlot = -1;
        InventoryType? firstContainer = null;
        bool? firstHq = null;
        var matchedSlots = 0;

        foreach (var containerType in RetainerInventoryTypes)
        {
            var container = inventoryManager->GetInventoryContainer(containerType);
            if (container == null)
            {
                continue;
            }

            for (var slotIndex = 0; slotIndex < container->Size; slotIndex++)
            {
                var slot = container->GetInventorySlot(slotIndex);
                if (slot == null)
                {
                    continue;
                }

                if (slot->ItemId != request.ItemId)
                {
                    continue;
                }

                if (slot->Quantity <= 0)
                {
                    continue;
                }

                var isHq = slot->IsHighQuality();
                if (request.Hq.HasValue && request.Hq.Value != isHq)
                {
                    continue;
                }

                matchedSlots++;
                totalAmount += slot->Quantity;

                if (firstSlot < 0)
                {
                    firstSlot = slotIndex;
                    firstContainer = containerType;
                    firstHq = isHq;
                }
            }
        }

        if (matchedSlots <= 0 || firstSlot < 0 || firstContainer == null)
        {
            result.Success = false;
            result.CanIdentifyRow = false;
            result.CanWithdraw = false;
            result.InventorySlot = null;
            result.InventoryContainer = null;
            result.AvailableAmount = 0;
            result.MatchedSlots = matchedSlots;
            result.Message = "PHASE4E1D_PAGE_GUARD_SCAN Target item was not found in retainer inventory containers.";
            return result;
        }

        var available = totalAmount;
        if (request.MaxAmount > 0)
        {
            available = Math.Min(available, request.MaxAmount);
        }

        result.Success = true;
        result.CanIdentifyRow = true;
        result.CanWithdraw = false;
        result.InventorySlot = firstSlot;
        result.InventoryContainer = firstContainer.Value.ToString();
        result.RetainerUiPageCandidate = GuessRetainerUiPage(firstContainer.Value);
        result.Hq = firstHq ?? request.Hq;
        result.AvailableAmount = available;
        result.MatchedSlots = matchedSlots;
        result.Message =
            $"PHASE4E1D_PAGE_GUARD_SCAN Target item row identified. Container={firstContainer.Value}; Slot={firstSlot}; Amount={available}; MatchedSlots={matchedSlots}; UiPageCandidate={result.RetainerUiPageCandidate}.";

        return result;
    }

    public static int? GuessRetainerUiPage(InventoryType inventoryType)
    {
        // Diagnostic heuristic:
        // FFXIVClientStructs exposes seven RetainerPage containers, while the user-facing
        // retainer inventory UI is commonly perceived as pages 1, 2, 3.
        // This mapping is intentionally conservative and only used for guard/logging.
        return inventoryType switch
        {
            InventoryType.RetainerPage1 => 1,
            InventoryType.RetainerPage2 => 1,
            InventoryType.RetainerPage3 => 1,
            InventoryType.RetainerPage4 => 2,
            InventoryType.RetainerPage5 => 2,
            InventoryType.RetainerPage6 => 2,
            InventoryType.RetainerPage7 => 3,
            InventoryType.RetainerCrystals => 3,
            _ => null,
        };
    }

    public static bool TryParseInventoryType(string? value, out InventoryType inventoryType)
    {
        inventoryType = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (Enum.TryParse(value, ignoreCase: true, out inventoryType))
        {
            return true;
        }

        if (int.TryParse(value, out var numeric))
        {
            inventoryType = (InventoryType)numeric;
            return true;
        }

        return false;
    }
}
