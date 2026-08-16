using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using SmartRecipeRestockHelper.Models;
using SmartRecipeRestockHelper.Services;

namespace SmartRecipeRestockHelper.Ipc;

public sealed class SmartRecipeRestockIpcProvider : IDisposable
{
    private readonly RetainerInventoryInspector _inspector;
    private readonly RetainerContextValidator _contextValidator;
    private readonly RetainerItemWithdrawer _withdrawer;

    private readonly ICallGateProvider<bool> _isAvailableProvider;
    private readonly ICallGateProvider<string> _getVersionProvider;
    private readonly ICallGateProvider<string> _getStatusProvider;
    private readonly ICallGateProvider<string> _validateRetainerContextProvider;
    private readonly ICallGateProvider<ulong, uint, int, int, string> _findRetainerItemProvider;
    private readonly ICallGateProvider<string, string> _previewWithdrawItemProvider;
    private readonly ICallGateProvider<string, string> _withdrawItemProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    public SmartRecipeRestockIpcProvider(
        IDalamudPluginInterface pluginInterface,
        RetainerInventoryInspector inspector,
        RetainerContextValidator contextValidator)
        : this(pluginInterface, inspector, contextValidator, new RetainerItemWithdrawer(inspector))
    {
    }

    public SmartRecipeRestockIpcProvider(
        IDalamudPluginInterface pluginInterface,
        RetainerInventoryInspector inspector,
        RetainerContextValidator contextValidator,
        RetainerItemWithdrawer withdrawer)
    {
        _inspector = inspector;
        _contextValidator = contextValidator;
        _withdrawer = withdrawer;

        _isAvailableProvider = pluginInterface.GetIpcProvider<bool>("SmartRecipeRestockHelper.IsAvailable");
        _getVersionProvider = pluginInterface.GetIpcProvider<string>("SmartRecipeRestockHelper.GetVersion");
        _getStatusProvider = pluginInterface.GetIpcProvider<string>("SmartRecipeRestockHelper.GetStatus");
        _validateRetainerContextProvider = pluginInterface.GetIpcProvider<string>("SmartRecipeRestockHelper.ValidateRetainerContext");
        _findRetainerItemProvider = pluginInterface.GetIpcProvider<ulong, uint, int, int, string>("SmartRecipeRestockHelper.FindRetainerItem");
        _previewWithdrawItemProvider = pluginInterface.GetIpcProvider<string, string>("SmartRecipeRestockHelper.PreviewWithdrawItem");
        _withdrawItemProvider = pluginInterface.GetIpcProvider<string, string>("SmartRecipeRestockHelper.WithdrawItem");

        _isAvailableProvider.RegisterFunc(IsAvailable);
        _getVersionProvider.RegisterFunc(GetVersion);
        _getStatusProvider.RegisterFunc(GetStatus);
        _validateRetainerContextProvider.RegisterFunc(ValidateRetainerContext);
        _findRetainerItemProvider.RegisterFunc(FindRetainerItem);
        _previewWithdrawItemProvider.RegisterFunc(PreviewWithdrawItemJson);
        _withdrawItemProvider.RegisterFunc(WithdrawItemJson);
    }

    public void Dispose()
    {
        _isAvailableProvider.UnregisterFunc();
        _getVersionProvider.UnregisterFunc();
        _getStatusProvider.UnregisterFunc();
        _validateRetainerContextProvider.UnregisterFunc();
        _findRetainerItemProvider.UnregisterFunc();
        _previewWithdrawItemProvider.UnregisterFunc();
        _withdrawItemProvider.UnregisterFunc();
    }

    private static bool IsAvailable()
    {
        return true;
    }

    private static string GetVersion()
    {
        return "0.1.14.0-PHASE4E1N-CHATQUEUE";
    }

    private string GetStatus()
    {
        var addonName = _inspector.DetectTransferAddonName();

        return JsonSerializer.Serialize(new
        {
            ReadOnly = false,
            WithdrawalEnabled = false,
            TransferScreenOpen = !string.IsNullOrEmpty(addonName),
            DetectedAddonName = addonName ?? string.Empty,
            Phase = "4E1n-ChatCommandQueue",
        }, JsonOptions);
    }

    private string ValidateRetainerContext()
    {
        var addonName = _inspector.DetectTransferAddonName();

        return JsonSerializer.Serialize(new
        {
            Success = !string.IsNullOrEmpty(addonName),
            Message = !string.IsNullOrEmpty(addonName)
                ? "Transfer screen detected."
                : "Transfer screen is not detected.",
            TransferScreenOpen = !string.IsNullOrEmpty(addonName),
            DetectedAddonName = addonName ?? string.Empty,
            CanWithdraw = false,
        }, JsonOptions);
    }

    private string FindRetainerItem(ulong retainerId, uint itemId, int hqFilter, int maxAmount)
    {
        var request = new RetainerItemSearchRequest
        {
            RetainerId = retainerId,
            ItemId = itemId,
            Hq = HqFilterToNullableBool(hqFilter),
            MaxAmount = maxAmount,
        };

        var result = _inspector.FindRetainerItem(request);
        result.CanWithdraw = false;

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private string PreviewWithdrawItemJson(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<RetainerItemWithdrawRequest>(requestJson, JsonOptions);
            if (request == null)
            {
                return JsonSerializer.Serialize(new RetainerItemWithdrawResult
                {
                    Success = false,
                    Message = "PHASE4E1N_PREVIEW_BLOCKED Invalid request JSON.",
                    CanWithdraw = false,
                    DidWithdraw = false,
                    DryRun = true,
                }, JsonOptions);
            }

            var result = _withdrawer.PreviewWithdraw(request);
            result.CanWithdraw = false;
            result.DidWithdraw = false;
            result.DryRun = true;

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new RetainerItemWithdrawResult
            {
                Success = false,
                Message = "PHASE4E1N_PREVIEW_BLOCKED Exception while previewing withdrawal.",
                Reason = ex.Message,
                CanWithdraw = false,
                DidWithdraw = false,
                DryRun = true,
            }, JsonOptions);
        }
    }

    private string WithdrawItemJson(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<RetainerItemWithdrawRequest>(requestJson, JsonOptions);
            if (request == null)
            {
                return JsonSerializer.Serialize(new RetainerItemWithdrawResult
                {
                    Success = false,
                    Message = "PHASE4E1N_WITHDRAW_BLOCKED Invalid request JSON.",
                    CanWithdraw = false,
                    DidWithdraw = false,
                    DryRun = true,
                }, JsonOptions);
            }

            var result = _withdrawer.Withdraw(request);
            result.CanWithdraw = false;

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new RetainerItemWithdrawResult
            {
                Success = false,
                Message = "PHASE4E1N_WITHDRAW_BLOCKED Exception while attempting withdrawal.",
                Reason = ex.Message,
                CanWithdraw = false,
                DidWithdraw = false,
                DryRun = true,
            }, JsonOptions);
        }
    }

    private static bool? HqFilterToNullableBool(int hqFilter)
    {
        return hqFilter switch
        {
            1 => true,
            0 => false,
            _ => null,
        };
    }
}
