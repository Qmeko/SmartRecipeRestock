using System.Text.Json;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

public sealed class RecipeProbeChatCommandHandler : IDisposable
{
    private const string CommandName = "/srrecipeprobe";
    private readonly ICommandManager _commandManager;
    private readonly RecipeContextDetector _detector;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
    };

    public RecipeProbeChatCommandHandler(
        ICommandManager commandManager,
        RecipeContextDetector detector)
    {
        _commandManager = commandManager;
        _detector = detector;

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "SmartRecipeRestock: probe current RecipeNote context and write cache file.",
        });
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var path = GetCachePath();

        RecipeContextResult result;
        try
        {
            result = _detector.GetCurrentRecipeContext();
        }
        catch (Exception ex)
        {
            result = new RecipeContextResult
            {
                Success = false,
                Source = "RecipeNote",
                DetectedAddonName = "RecipeNote",
                Message = "PHASE5C_RECIPE_PROBE Command exception.",
                Reason = ex.GetType().Name + ":" + ex.Message,
            };
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (Exception ex)
        {
            var failPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "recipe_context_probe_failed.txt");

            File.WriteAllText(
                failPath,
                "Failed to write RecipeNote probe cache." + Environment.NewLine
                + ex.GetType().Name + ": " + ex.Message + Environment.NewLine
                + JsonSerializer.Serialize(result, JsonOptions));
        }
    }

    private static string GetCachePath()
    {
        return @"I:\ExtractMat\SmartRecipeRestockWorkspace\cache\recipe_context_probe.json";
    }
}
