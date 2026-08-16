using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SmartRecipeRestockHelper.UI;

public sealed class StandaloneWindowCommandHandler : IDisposable
{
    public const string CommandName = "/srr";
    public const string CommandNameLong = "/smartreciperestock";

    private readonly ICommandManager _commandManager;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly WindowSystem _windowSystem;
    private readonly MainWindow _window;

    public StandaloneWindowCommandHandler(
        ICommandManager commandManager,
        IDalamudPluginInterface pluginInterface,
        MainWindow window)
    {
        _commandManager = commandManager;
        _pluginInterface = pluginInterface;
        _window = window;
        _windowSystem = new WindowSystem("SmartRecipeRestockHelper");
        _windowSystem.AddWindow(_window);

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Smart Recipe Restock の窓を開く / 閉じる",
        });
        _commandManager.AddHandler(CommandNameLong, new CommandInfo(OnCommand)
        {
            HelpMessage = "Smart Recipe Restock の窓を開く / 閉じる",
        });

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += Toggle;
    }

    public void Dispose()
    {
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi -= Toggle;
        _commandManager.RemoveHandler(CommandName);
        _commandManager.RemoveHandler(CommandNameLong);
        _windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string args) => Toggle();

    private void Toggle() => _window.IsOpen = !_window.IsOpen;
}
