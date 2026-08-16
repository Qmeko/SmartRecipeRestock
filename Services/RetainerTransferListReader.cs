using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SmartRecipeRestockHelper.Services;

/// <summary>Read-only scan of inventory number arrays for item IDs (AutoRetainer transfer-list pattern).</summary>
public sealed unsafe class RetainerTransferListReader : AtkAddonReader
{
    private readonly NumberArrayData* _numberArray;

    public RetainerTransferListReader(NumberArrayData* numberArray, int beginOffset = 0)
        : base(numberArray, beginOffset)
    {
        _numberArray = numberArray;
    }

    public IReadOnlyList<TransferListEntry> ReadItems()
    {
        if (_numberArray == null)
        {
            return Array.Empty<TransferListEntry>();
        }

        var items = new List<TransferListEntry>();
        var span = _numberArray->Span;
        for (var i = 1; i < span.Length; i++)
        {
            var raw = (uint)Math.Max(0, span[i]);
            if (raw == 0)
            {
                continue;
            }

            items.Add(new TransferListEntry
            {
                ListIndex = i,
                ItemId = raw > 1_000_000 ? raw - 1_000_000 : raw,
                IsHq = raw > 1_000_000,
            });
        }

        return items;
    }
}

public sealed class TransferListEntry
{
    public int ListIndex { get; init; }
    public uint ItemId { get; init; }
    public bool IsHq { get; init; }
}
