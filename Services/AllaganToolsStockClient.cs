using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace SmartRecipeRestockHelper.Services;

public sealed class AllaganToolsStockClient
{
    private readonly IPluginLog _log;
    private readonly ICallGateSubscriber<bool>? _isInitialized;
    private readonly ICallGateSubscriber<uint, ulong, int, uint>? _itemCount;

    public AllaganToolsStockClient(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;

        try
        {
            _isInitialized = pluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
            _itemCount = pluginInterface.GetIpcSubscriber<uint, ulong, int, uint>("AllaganTools.ItemCount");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "AllaganTools IPC subscriber setup failed.");
        }
    }

    public bool IsAvailable
    {
        get
        {
            if (_isInitialized == null || _itemCount == null)
            {
                return false;
            }

            try
            {
                return _isInitialized.InvokeFunc();
            }
            catch (IpcNotReadyError)
            {
                return false;
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "AllaganTools.IsInitialized failed.");
                return false;
            }
        }
    }

    public int GetItemCount(uint itemId, ulong retainerId)
    {
        if (_itemCount == null || itemId == 0 || retainerId == 0)
        {
            return 0;
        }

        try
        {
            return (int)_itemCount.InvokeFunc(itemId, retainerId, -1);
        }
        catch (IpcNotReadyError)
        {
            return 0;
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "AllaganTools.ItemCount failed. ItemId={ItemId}; RetainerId={RetainerId}", itemId, retainerId);
            return 0;
        }
    }
}
