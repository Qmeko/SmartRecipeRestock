using System.Text.Json;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

public sealed unsafe class RetainerInventoryCacheWriter
{
    private readonly IPluginLog _log;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
    };

    public RetainerInventoryCacheWriter(IPluginLog log)
    {
        _log = log;
    }

    public string CachePath => @"I:\ExtractMat\SmartRecipeRestockWorkspace\cache\retainer_inventory_cache.json";

    public IReadOnlyList<RetainerInventoryCacheItem> ScanCurrentRetainer(ulong retainerId, string retainerName)
    {
        var now = DateTimeOffset.Now;
        var results = new List<RetainerInventoryCacheItem>();

        var manager = InventoryManager.Instance();
        if (manager == null)
        {
            _log.Warning("SmartRecipeRestock cache scan failed: InventoryManager.Instance() returned null.");
            return results;
        }

        foreach (var inventoryType in GetRetainerInventoryTypes())
        {
            var container = manager->GetInventoryContainer(inventoryType);
            if (container == null)
            {
                continue;
            }

            var size = container->Size;
            if (size <= 0)
            {
                continue;
            }

            for (var slotIndex = 0; slotIndex < size; slotIndex++)
            {
                var slot = container->GetInventorySlot(slotIndex);
                if (slot == null)
                {
                    continue;
                }

                var itemId = slot->ItemId;
                var quantity = slot->Quantity;
                if (itemId == 0 || quantity <= 0)
                {
                    continue;
                }

                results.Add(new RetainerInventoryCacheItem
                {
                    RetainerId = retainerId,
                    RetainerName = retainerName ?? string.Empty,
                    ItemId = itemId,
                    HighQuality = slot->IsHighQuality(),
                    Amount = (uint)quantity,
                    InventoryType = inventoryType.ToString(),
                    UiPage = ToUiPage(inventoryType),
                    Slot = slotIndex,
                    ScannedAt = now,
                });
            }
        }

        return results;
    }

    public RetainerInventoryCacheFile MergeAndWriteCurrentRetainer(ulong retainerId, string retainerName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);

        var scanned = ScanCurrentRetainer(retainerId, retainerName);
        var file = ReadExisting();

        // Replace only this retainer's rows. Other retainers remain cached.
        file.Items = file.Items
            .Where(item => item.RetainerId != retainerId)
            .Concat(scanned)
            .OrderBy(item => item.RetainerName)
            .ThenBy(item => item.RetainerId)
            .ThenBy(item => item.ItemId)
            .ThenBy(item => item.HighQuality)
            .ThenBy(item => item.InventoryType)
            .ThenBy(item => item.Slot)
            .ToList();

        file.SchemaVersion = "1.0";
        file.UpdatedAt = DateTimeOffset.Now;

        File.WriteAllText(CachePath, JsonSerializer.Serialize(file, JsonOptions));

        _log.Information(
            "SmartRecipeRestock cache updated. retainerId={RetainerId} retainerName={RetainerName} rows={Rows} path={Path}",
            retainerId,
            retainerName,
            scanned.Count,
            CachePath);

        return file;
    }

    private RetainerInventoryCacheFile ReadExisting()
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return new RetainerInventoryCacheFile();
            }

            var text = File.ReadAllText(CachePath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new RetainerInventoryCacheFile();
            }

            return JsonSerializer.Deserialize<RetainerInventoryCacheFile>(text, JsonOptions)
                   ?? new RetainerInventoryCacheFile();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "SmartRecipeRestock cache read failed. Rebuilding cache file.");
            return new RetainerInventoryCacheFile();
        }
    }

    private static IEnumerable<InventoryType> GetRetainerInventoryTypes()
    {
        yield return InventoryType.RetainerPage1;
        yield return InventoryType.RetainerPage2;
        yield return InventoryType.RetainerPage3;
        yield return InventoryType.RetainerPage4;
        yield return InventoryType.RetainerPage5;
        yield return InventoryType.RetainerPage6;
        yield return InventoryType.RetainerPage7;
        yield return InventoryType.RetainerCrystals;
    }

    private static int ToUiPage(InventoryType inventoryType)
    {
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
            _ => 0,
        };
    }
}


