using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using SmartRecipeRestockHelper.Models;
using SmartRecipeRestockHelper.Services;

namespace SmartRecipeRestockHelper.Ipc;

public sealed class SmartRecipeRestockRecipeIpcProvider : IDisposable
{
    private readonly RecipeContextDetector _recipeContextDetector;
    private readonly ICallGateProvider<string> _getCurrentRecipeContextProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    public SmartRecipeRestockRecipeIpcProvider(
        IDalamudPluginInterface pluginInterface,
        RecipeContextDetector recipeContextDetector)
    {
        _recipeContextDetector = recipeContextDetector;
        _getCurrentRecipeContextProvider =
            pluginInterface.GetIpcProvider<string>("SmartRecipeRestockHelper.GetCurrentRecipeContext");

        _getCurrentRecipeContextProvider.RegisterFunc(GetCurrentRecipeContextJson);
    }

    public void Dispose()
    {
        _getCurrentRecipeContextProvider.UnregisterFunc();
    }

    private string GetCurrentRecipeContextJson()
    {
        try
        {
            var result = _recipeContextDetector.GetCurrentRecipeContext();
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new RecipeContextResult
            {
                Success = false,
                Source = "RecipeNote",
                DetectedAddonName = "RecipeNote",
                Message = "PHASE5A_RECIPE_DETECT Exception in IPC provider.",
                Reason = ex.Message,
            }, JsonOptions);
        }
    }
}
