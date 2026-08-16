using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace SmartRecipeRestockHelper.Services;

public sealed unsafe class RetainerUiNavigator
{
    private readonly IGameGui _gameGui;
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    public RetainerUiNavigator(IGameGui gameGui, IDataManager data, IPluginLog log)
    {
        _gameGui = gameGui;
        _data = data;
        _log = log;
    }

    public bool IsRetainerListOpen => IsAddonReady("RetainerList");

    public bool IsSelectStringOpen => IsAddonReady("SelectString");

    public bool IsTransferOpen =>
        IsAddonReady("InventoryRetainerLarge")
        || IsAddonReady("InventoryRetainer")
        || IsAddonReady("Inventory");

    public bool TrySelectRetainer(int listIndex)
    {
        if (listIndex < 0)
        {
            return false;
        }

        return TryFire("RetainerList", 2, listIndex);
    }

    public bool TryOpenItemTransfer()
    {
        var label = GetAddonText(2378);
        if (TrySelectStringEntry(label))
        {
            return true;
        }

        return TryFire("SelectString", 0);
    }

    public bool TryQuitRetainer()
    {
        var label = GetAddonText(2383);
        if (TrySelectStringEntry(label))
        {
            return true;
        }

        return TryFire("SelectString", -1);
    }

    public bool TryCloseTransfer()
    {
        foreach (var name in new[] { "InventoryRetainerLarge", "InventoryRetainer", "Inventory" })
        {
            if (IsAddonReady(name) && TryFire(name, -1))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsAddonReady(string name)
    {
        var addon = GetAddon(name);
        return addon != null && addon->IsVisible;
    }

    private bool TrySelectStringEntry(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var addon = (AddonSelectString*)GetAddon("SelectString");
        if (addon == null || !addon->AtkUnitBase.IsVisible)
        {
            return false;
        }

        var entries = ReadSelectStringEntries(addon);
        var index = entries.FindIndex(text =>
            text.Contains(label, StringComparison.OrdinalIgnoreCase)
            || label.Contains(text, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return false;
        }

        return TryFire("SelectString", index);
    }

    private List<string> ReadSelectStringEntries(AddonSelectString* addon)
    {
        var list = new List<string>();
        try
        {
            var count = addon->PopupMenu.PopupMenu.EntryCount;
            for (var i = 0; i < count; i++)
            {
                var namePtr = addon->PopupMenu.PopupMenu.EntryNames[i].Value;
                if (namePtr == null)
                {
                    list.Add(string.Empty);
                    continue;
                }

                var text = MemoryHelper.ReadSeStringNullTerminated((nint)namePtr).TextValue;
                list.Add(text);
            }
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Failed to read SelectString entries.");
        }

        return list;
    }

    private bool TryFire(string addonName, params int[] values)
    {
        var addon = GetAddon(addonName);
        if (addon == null)
        {
            return false;
        }

        try
        {
            var count = values.Length;
            var atkValues = stackalloc AtkValue[count];
            for (var i = 0; i < count; i++)
            {
                atkValues[i].SetInt(values[i]);
            }

            addon->FireCallback((uint)count, atkValues, true);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "FireCallback failed. Addon={Addon}", addonName);
            return false;
        }
    }

    private AtkUnitBase* GetAddon(string name)
    {
        var addon = _gameGui.GetAddonByName(name);
        if (addon.Address == nint.Zero)
        {
            return null;
        }

        return (AtkUnitBase*)addon.Address;
    }

    private string GetAddonText(uint rowId)
    {
        var sheet = _data.GetExcelSheet<Addon>();
        if (sheet != null && sheet.TryGetRow(rowId, out var row))
        {
            return row.Text.ToString();
        }

        return string.Empty;
    }
}
