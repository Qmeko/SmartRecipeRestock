using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

namespace SmartRecipeRestockHelper.Services;

public sealed class RetainerInventoryCacheChatCommandHandler : IDisposable
{
    private const string CommandName = "/srrcachecurrent";

    private readonly ICommandManager _commandManager;
    private readonly RetainerInventoryCacheWriter _cacheWriter;

    public RetainerInventoryCacheChatCommandHandler(
        ICommandManager commandManager,
        RetainerInventoryCacheWriter cacheWriter)
    {
        _commandManager = commandManager;
        _cacheWriter = cacheWriter;

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "SmartRecipeRestock: cache currently opened retainer inventory. Usage: /srrcachecurrent <retainerId> [retainerName]",
        });
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var parsed = ParseArgs(args);
        var retainerId = parsed.RetainerId;
        var retainerName = parsed.RetainerName;

        _cacheWriter.MergeAndWriteCurrentRetainer(retainerId, retainerName);
    }

    private static ParsedArgs ParseArgs(string args)
    {
        args = (args ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(args))
        {
            return new ParsedArgs(0, string.Empty);
        }

        var firstSpace = args.IndexOf(' ');
        var idText = firstSpace >= 0 ? args[..firstSpace].Trim() : args;
        var name = firstSpace >= 0 ? args[(firstSpace + 1)..].Trim() : string.Empty;

        if (!ulong.TryParse(idText, out var retainerId))
        {
            // Allow missing id when manually testing. It will overwrite id=0 cache rows.
            retainerId = 0;
            name = args;
        }

        return new ParsedArgs(retainerId, name);
    }

    private readonly record struct ParsedArgs(ulong RetainerId, string RetainerName);
}

