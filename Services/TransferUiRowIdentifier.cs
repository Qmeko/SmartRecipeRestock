using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SmartRecipeRestockHelper.Services;

public sealed unsafe class TransferUiRowIdentifier
{
    private static readonly (string AddonName, NumberArrayType ArrayType)[] ArrayProbeOrder =
    [
        ("Inventory", NumberArrayType.Inventory),
        ("InventoryRetainer", NumberArrayType.InventoryRetainer),
        ("InventoryRetainerLarge", NumberArrayType.InventoryRetainer),
        ("RetainerItemTransferList", NumberArrayType.InventoryRetainer),
    ];

    private readonly IGameGui _gameGui;

    public TransferUiRowIdentifier(IGameGui gameGui)
    {
        _gameGui = gameGui;
    }

    public int? TryIdentify(
        string detectedAddon,
        uint itemId,
        bool? hqFilter,
        InventoryType memoryContainer,
        int memorySlot)
    {
        var expectedGlobalSlot = ToGlobalRetainerSlot(memoryContainer, memorySlot);
        var probes = BuildProbeOrder(detectedAddon);

        foreach (var (addonName, arrayType) in probes)
        {
            if (_gameGui.GetAddonByName(addonName).Address == nint.Zero)
            {
                continue;
            }

            var listIndex = TryReadArrayListIndex(arrayType, itemId, hqFilter, expectedGlobalSlot);
            if (listIndex.HasValue)
            {
                return listIndex.Value;
            }
        }

        return null;
    }

    private static IEnumerable<(string AddonName, NumberArrayType ArrayType)> BuildProbeOrder(string detectedAddon)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<(string, NumberArrayType)>();

        void Add(string? addonName, NumberArrayType arrayType)
        {
            if (string.IsNullOrWhiteSpace(addonName) || !seen.Add(addonName))
            {
                return;
            }

            ordered.Add((addonName, arrayType));
        }

        var detectedType = ResolveArrayType(detectedAddon);
        if (detectedType.HasValue)
        {
            Add(detectedAddon, detectedType.Value);
        }

        foreach (var probe in ArrayProbeOrder)
        {
            Add(probe.AddonName, probe.ArrayType);
        }

        return ordered;
    }

    private static NumberArrayType? ResolveArrayType(string addonName)
    {
        return addonName switch
        {
            "Inventory" => NumberArrayType.Inventory,
            "InventoryRetainer" => NumberArrayType.InventoryRetainer,
            "InventoryRetainerLarge" => NumberArrayType.InventoryRetainer,
            "RetainerItemTransferList" => NumberArrayType.InventoryRetainer,
            _ => null,
        };
    }

    private static int? TryReadArrayListIndex(
        NumberArrayType arrayType,
        uint itemId,
        bool? hqFilter,
        int? expectedGlobalSlot)
    {
        var atkStage = AtkStage.Instance();
        if (atkStage == null)
        {
            return null;
        }

        var numberArray = atkStage->GetNumberArrayData(arrayType);
        if (numberArray == null)
        {
            return null;
        }

        var reader = new RetainerTransferListReader(numberArray);
        var matches = reader.ReadItems()
            .Where(entry => entry.ItemId == itemId && MatchesHq(entry.IsHq, hqFilter))
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        if (matches.Count == 1)
        {
            return matches[0].ListIndex;
        }

        if (expectedGlobalSlot.HasValue)
        {
            var correlated = matches.FirstOrDefault(entry => entry.ListIndex == expectedGlobalSlot.Value);
            if (correlated != null)
            {
                return correlated.ListIndex;
            }
        }

        return null;
    }

    private static bool MatchesHq(bool isHq, bool? hqFilter)
    {
        return hqFilter switch
        {
            true => isHq,
            false => !isHq,
            _ => true,
        };
    }

    private static int? ToGlobalRetainerSlot(InventoryType container, int slot)
    {
        if (slot < 0)
        {
            return null;
        }

        int? pageIndex = container switch
        {
            InventoryType.RetainerPage1 => 0,
            InventoryType.RetainerPage2 => 1,
            InventoryType.RetainerPage3 => 2,
            InventoryType.RetainerPage4 => 3,
            InventoryType.RetainerPage5 => 4,
            InventoryType.RetainerPage6 => 5,
            InventoryType.RetainerPage7 => 6,
            InventoryType.RetainerCrystals => 7,
            _ => null,
        };

        if (!pageIndex.HasValue)
        {
            return null;
        }

        const int slotsPerPage = 25;
        return (pageIndex.Value * slotsPerPage) + slot;
    }
}
