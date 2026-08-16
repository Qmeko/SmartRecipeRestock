using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SmartRecipeRestockHelper.Ipc;
using SmartRecipeRestockHelper.Services;
using SmartRecipeRestockHelper.UI;

namespace SmartRecipeRestockHelper;

public sealed class Plugin : IDalamudPlugin
{
    private readonly RetainerWithdrawCommandQueue _commandQueue;
    private readonly RetainerQueueChatCommandHandler _queueChatCommandHandler;
    private readonly SmartRecipeRestockIpcProvider _ipcProvider;
    private readonly RetainerInventoryCacheChatCommandHandler _retainerInventoryCacheChatCommandHandler;
    private readonly SmartRecipeRestockRecipeIpcProvider _recipeIpcProvider;
    private readonly RecipeProbeChatCommandHandler _recipeProbeChatCommandHandler;
    private readonly StandaloneRestockService _restockService;
    private readonly StandaloneWindowCommandHandler _windowCommandHandler;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        IGameGui gameGui,
        ICondition condition,
        ISigScanner sigScanner,
        IFramework framework,
        ICommandManager commandManager,
        IDataManager dataManager)
    {
        var inspector = new RetainerInventoryInspector(gameGui, condition);
        var retainerInventoryCacheWriter = new RetainerInventoryCacheWriter(log);
        var recipeContextDetector = new RecipeContextDetector(gameGui);
        var contextValidator = new RetainerContextValidator(condition, inspector);
        var commandExecutor = new RetainerItemCommandExecutor(sigScanner, gameGui, log);
        _commandQueue = new RetainerWithdrawCommandQueue(framework, commandExecutor, log);
        _queueChatCommandHandler = new RetainerQueueChatCommandHandler(commandManager, inspector, _commandQueue, log);
        _retainerInventoryCacheChatCommandHandler = new RetainerInventoryCacheChatCommandHandler(commandManager, retainerInventoryCacheWriter);
        _recipeProbeChatCommandHandler = new RecipeProbeChatCommandHandler(commandManager, recipeContextDetector);
        var withdrawer = new RetainerItemWithdrawer(inspector, commandExecutor, _commandQueue);

        _ipcProvider = new SmartRecipeRestockIpcProvider(pluginInterface, inspector, contextValidator, withdrawer);
        _recipeIpcProvider = new SmartRecipeRestockRecipeIpcProvider(pluginInterface, recipeContextDetector);

        var materialReader = new RecipeMaterialReader(dataManager);
        var playerInventory = new PlayerInventoryCounter();
        var retainerDirectory = new RetainerDirectory();
        var allagan = new AllaganToolsStockClient(pluginInterface, log);
        var ui = new RetainerUiNavigator(gameGui, dataManager, log);
        _restockService = new StandaloneRestockService(
            recipeContextDetector,
            materialReader,
            playerInventory,
            inspector,
            retainerDirectory,
            allagan,
            ui,
            _commandQueue,
            framework,
            log);
        var window = new MainWindow(_restockService, inspector);
        _windowCommandHandler = new StandaloneWindowCommandHandler(commandManager, pluginInterface, window);

        log.Information("SmartRecipeRestockHelper loaded as a standalone plugin. Command: /srr");
    }

    public void Dispose()
    {
        _windowCommandHandler.Dispose();
        _restockService.Dispose();
        _recipeProbeChatCommandHandler.Dispose();
        _recipeIpcProvider.Dispose();
        _retainerInventoryCacheChatCommandHandler.Dispose();
        _ipcProvider.Dispose();
        _queueChatCommandHandler.Dispose();
        _commandQueue.Dispose();
    }
}
