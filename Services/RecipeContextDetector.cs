using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using SmartRecipeRestockHelper.Models;
using GameRecipeNote = FFXIVClientStructs.FFXIV.Client.Game.UI.RecipeNote;

namespace SmartRecipeRestockHelper.Services;

public sealed unsafe class RecipeContextDetector
{
    private readonly IGameGui _gameGui;

    public RecipeContextDetector(IGameGui gameGui)
    {
        _gameGui = gameGui;
    }

    public RecipeContextResult GetCurrentRecipeContext()
    {
        var result = new RecipeContextResult
        {
            Success = false,
            Source = "Game.UI.RecipeNote.RecipeList.SelectedRecipe",
            DetectedAddonName = "RecipeNote",
            IsRecipeUiOpen = false,
        };

        try
        {
            var addon = _gameGui.GetAddonByName("RecipeNote");
            if (addon.Address == nint.Zero)
            {
                result.Message = "PHASE5D2_RECIPE_DETECT RecipeNote addon is not open.";
                result.Reason = "RecipeNote addon not found.";
                return result;
            }

            var addonRecipeNote = (AddonRecipeNote*)addon.Address;
            if (addonRecipeNote == null || !addonRecipeNote->AtkUnitBase.IsVisible)
            {
                result.Message = "PHASE5D2_RECIPE_DETECT RecipeNote addon exists but is not visible.";
                result.Reason = $"RecipeNote addon address=0x{addon.Address:X}; visible=false.";
                return result;
            }

            result.IsRecipeUiOpen = true;

            var recipeNote = GameRecipeNote.Instance();
            if (recipeNote == null)
            {
                result.Message = "PHASE5D2_RECIPE_DETECT Game.UI.RecipeNote.Instance() returned null.";
                result.Reason = "Game.UI.RecipeNote singleton unavailable.";
                return result;
            }

            var isReady = recipeNote->IsRecipeListReady;
            var activeRecipeId = (uint)recipeNote->ActiveCraftRecipeId;

            if (recipeNote->RecipeList == null)
            {
                result.Message = "PHASE5D2_RECIPE_DETECT RecipeList is null.";
                result.Reason = $"IsRecipeListReady={isReady}; ActiveCraftRecipeId={activeRecipeId}; RecipeList=null.";
                return result;
            }

            var recipeCount = recipeNote->RecipeList->RecipeCount;
            var selectedIndex = recipeNote->RecipeList->SelectedIndex;
            var selectedRecipe = recipeNote->RecipeList->SelectedRecipe;

            if (selectedRecipe == null)
            {
                result.Message = "PHASE5D2_RECIPE_DETECT SelectedRecipe is null.";
                result.Reason = $"IsRecipeListReady={isReady}; RecipeCount={recipeCount}; SelectedIndex={selectedIndex}; ActiveCraftRecipeId={activeRecipeId}.";
                return result;
            }

            var selectedRecipeId = (uint)selectedRecipe->RecipeId;
            var selectedItemId = (uint)selectedRecipe->ItemId;

            if (selectedRecipeId != 0)
            {
                result.Success = true;
                result.RecipeId = selectedRecipeId;
                result.ResultItemId = selectedItemId;
                result.Message = "PHASE5D2_RECIPE_DETECT Current recipeId detected from RecipeList.SelectedRecipe.";
                result.Reason = $"IsRecipeListReady={isReady}; RecipeCount={recipeCount}; SelectedIndex={selectedIndex}; SelectedRecipeId={selectedRecipeId}; SelectedItemId={selectedItemId}; ActiveCraftRecipeId={activeRecipeId}; addon=0x{addon.Address:X}.";
                return result;
            }

            if (activeRecipeId != 0)
            {
                result.Success = true;
                result.RecipeId = activeRecipeId;
                result.ResultItemId = selectedItemId;
                result.Source = "Game.UI.RecipeNote.ActiveCraftRecipeId";
                result.Message = "PHASE5D2_RECIPE_DETECT Current recipeId detected from ActiveCraftRecipeId fallback.";
                result.Reason = $"IsRecipeListReady={isReady}; RecipeCount={recipeCount}; SelectedIndex={selectedIndex}; SelectedRecipeId=0; SelectedItemId={selectedItemId}; ActiveCraftRecipeId={activeRecipeId}; addon=0x{addon.Address:X}.";
                return result;
            }

            result.Message = "PHASE5D2_RECIPE_DETECT SelectedRecipe exists, but recipe ids are zero.";
            result.Reason = $"IsRecipeListReady={isReady}; RecipeCount={recipeCount}; SelectedIndex={selectedIndex}; SelectedRecipeId=0; SelectedItemId={selectedItemId}; ActiveCraftRecipeId={activeRecipeId}.";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = "PHASE5D2_RECIPE_DETECT Exception while detecting current recipe.";
            result.Reason = ex.GetType().Name + ": " + ex.Message;
            return result;
        }
    }
}
