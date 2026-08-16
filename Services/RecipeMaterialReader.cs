using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

public sealed class RecipeMaterialReader
{
    public const uint CrystalItemIdMin = 2;
    public const uint CrystalItemIdMax = 19;

    private readonly IDataManager _data;

    public RecipeMaterialReader(IDataManager data)
    {
        _data = data;
    }

    public bool TryRead(uint recipeId, out uint resultItemId, out string resultItemName, out List<RecipeMaterial> materials, out string error)
    {
        resultItemId = 0;
        resultItemName = string.Empty;
        materials = [];
        error = string.Empty;

        if (recipeId == 0)
        {
            error = "レシピIDが 0 です。製作ノートでレシピを選んでください。";
            return false;
        }

        var sheet = _data.GetExcelSheet<Recipe>();
        if (sheet == null)
        {
            error = "レシピ表を読めませんでした。";
            return false;
        }

        if (!sheet.TryGetRow(recipeId, out var recipe))
        {
            error = $"レシピID {recipeId} が見つかりません。";
            return false;
        }

        resultItemId = recipe.ItemResult.RowId;
        resultItemName = SafeItemName(recipe.ItemResult.RowId, recipe.ItemResult.ValueNullable?.Name.ToString());

        for (var i = 0; i < recipe.Ingredient.Count; i++)
        {
            var itemId = recipe.Ingredient[i].RowId;
            var amount = recipe.AmountIngredient[i];
            if (itemId == 0 || amount <= 0)
            {
                continue;
            }

            materials.Add(new RecipeMaterial
            {
                ItemId = itemId,
                Name = SafeItemName(itemId, recipe.Ingredient[i].ValueNullable?.Name.ToString()),
                AmountPerCraft = amount,
                IsCrystal = itemId >= CrystalItemIdMin && itemId <= CrystalItemIdMax,
            });
        }

        if (materials.Count == 0)
        {
            error = "このレシピから材料を読めませんでした。";
            return false;
        }

        return true;
    }

    public string GetItemName(uint itemId)
    {
        if (itemId == 0)
        {
            return string.Empty;
        }

        var sheet = _data.GetExcelSheet<Item>();
        if (sheet != null && sheet.TryGetRow(itemId, out var item))
        {
            var name = item.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return $"Item {itemId}";
    }

    private string SafeItemName(uint itemId, string? excelName)
    {
        if (!string.IsNullOrWhiteSpace(excelName))
        {
            return excelName;
        }

        return GetItemName(itemId);
    }
}
