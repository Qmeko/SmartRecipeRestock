using Dalamud.Plugin.Services;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

public sealed class StandaloneRestockService : IDisposable
{
    private const int WithdrawDelayMs = 700;
    private const int UiStepDelayMs = 500;
    private const int WaitTimeoutMs = 8000;

    private readonly RecipeContextDetector _recipeDetector;
    private readonly RecipeMaterialReader _materialReader;
    private readonly PlayerInventoryCounter _playerInventory;
    private readonly RetainerInventoryInspector _inspector;
    private readonly RetainerDirectory _retainers;
    private readonly AllaganToolsStockClient _allagan;
    private readonly RetainerUiNavigator _ui;
    private readonly RetainerWithdrawCommandQueue _queue;
    private readonly IFramework _framework;
    private readonly IPluginLog _log;

    private readonly Queue<RetainerVisitPlan> _visits = new();
    private readonly Queue<uint> _withdrawItemIds = new();
    private Phase _phase = Phase.Idle;
    private RetainerVisitPlan? _currentVisit;
    private long _phaseStartedTickMs;
    private long _nextReadyTickMs;
    private bool _subscribed;
    private int _withdrawnCount;
    private int _skippedCount;
    private int _visitedCount;

    public StandaloneRestockService(
        RecipeContextDetector recipeDetector,
        RecipeMaterialReader materialReader,
        PlayerInventoryCounter playerInventory,
        RetainerInventoryInspector inspector,
        RetainerDirectory retainers,
        AllaganToolsStockClient allagan,
        RetainerUiNavigator ui,
        RetainerWithdrawCommandQueue queue,
        IFramework framework,
        IPluginLog log)
    {
        _recipeDetector = recipeDetector;
        _materialReader = materialReader;
        _playerInventory = playerInventory;
        _inspector = inspector;
        _retainers = retainers;
        _allagan = allagan;
        _ui = ui;
        _queue = queue;
        _framework = framework;
        _log = log;
    }

    public bool IsRunning => _phase != Phase.Idle;

    public string Status { get; private set; } = "製作ノートでレシピを選んで「レシピを読み取る」を押してください。";

    public uint RecipeId { get; private set; }

    public string RecipeName { get; private set; } = string.Empty;

    public uint ResultItemId { get; private set; }

    public string ResultItemName { get; private set; } = string.Empty;

    public int CraftCount { get; private set; } = 1;

    public bool AllaganToolsAvailable { get; private set; }

    public List<RestockMaterialRow> Rows { get; private set; } = [];

    public List<RetainerVisitPlan> VisitPlans { get; private set; } = [];

    public bool RefreshPlan(int craftCount)
    {
        if (IsRunning)
        {
            Status = "取り出し中です。終わるまで待ってください。";
            return false;
        }

        craftCount = Math.Clamp(craftCount, 1, 999);
        CraftCount = craftCount;

        var context = _recipeDetector.GetCurrentRecipeContext();
        if (!context.Success || context.RecipeId is not uint recipeId || recipeId == 0)
        {
            ClearPlan();
            Status = context.IsRecipeUiOpen
                ? "製作ノートは開いていますが、レシピを選べていません。"
                : "製作ノートを開いて、レシピを選んでください。";
            return false;
        }

        if (!_materialReader.TryRead(recipeId, out var resultItemId, out var resultItemName, out var materials, out var error))
        {
            ClearPlan();
            Status = error;
            return false;
        }

        RecipeId = recipeId;
        ResultItemId = resultItemId;
        ResultItemName = resultItemName;
        RecipeName = string.IsNullOrWhiteSpace(resultItemName) ? $"Recipe {recipeId}" : resultItemName;

        var retainers = _retainers.GetCurrentCharacterRetainers();
        AllaganToolsAvailable = _allagan.IsAvailable;
        var assignments = new Dictionary<ulong, List<uint>>();
        var sourceNames = new Dictionary<uint, List<string>>();

        foreach (var retainer in retainers)
        {
            assignments[retainer.RetainerId] = [];
        }

        var rows = new List<RestockMaterialRow>(materials.Count);
        foreach (var material in materials)
        {
            var needed = material.AmountPerCraft * craftCount;
            var playerCount = _playerInventory.Count(material.ItemId);
            var missing = Math.Max(0, needed - playerCount);
            var remaining = missing;
            var knownStock = 0;

            if (!material.IsCrystal && missing > 0)
            {
                foreach (var retainer in retainers)
                {
                    var stock = AllaganToolsAvailable
                        ? _allagan.GetItemCount(material.ItemId, retainer.RetainerId)
                        : 0;
                    knownStock += stock;

                    if (AllaganToolsAvailable && remaining > 0 && stock > 0)
                    {
                        assignments[retainer.RetainerId].Add(material.ItemId);
                        remaining = Math.Max(0, remaining - stock);
                        if (!sourceNames.TryGetValue(material.ItemId, out var names))
                        {
                            names = [];
                            sourceNames[material.ItemId] = names;
                        }

                        names.Add(retainer.Name);
                    }
                }
            }

            string rowStatus;
            if (material.IsCrystal)
            {
                rowStatus = "クリスタルは対象外";
            }
            else if (missing <= 0)
            {
                rowStatus = "足りている";
            }
            else if (retainers.Count == 0)
            {
                rowStatus = "リテイナー一覧を読めない";
            }
            else if (AllaganToolsAvailable && knownStock <= 0)
            {
                rowStatus = "どのリテイナーにも無い";
            }
            else if (AllaganToolsAvailable)
            {
                rowStatus = remaining > 0 ? "一部だけ見つかった" : "リテイナーから取れる";
            }
            else
            {
                rowStatus = "在庫不明。全リテイナーを確認する";
            }

            rows.Add(new RestockMaterialRow
            {
                ItemId = material.ItemId,
                Name = material.Name,
                IsCrystal = material.IsCrystal,
                AmountPerCraft = material.AmountPerCraft,
                Needed = needed,
                PlayerCount = playerCount,
                RetainerCount = knownStock,
                Missing = missing,
                CanWithdrawNow = !material.IsCrystal && missing > 0 && (AllaganToolsAvailable ? knownStock > 0 : retainers.Count > 0),
                SourceRetainers = sourceNames.TryGetValue(material.ItemId, out var from)
                    ? string.Join(", ", from)
                    : AllaganToolsAvailable || material.IsCrystal || missing <= 0
                        ? string.Empty
                        : "全リテイナー確認",
                Status = rowStatus,
            });
        }

        var visits = new List<RetainerVisitPlan>();
        if (AllaganToolsAvailable)
        {
            foreach (var retainer in retainers)
            {
                var itemIds = assignments[retainer.RetainerId];
                if (itemIds.Count == 0)
                {
                    continue;
                }

                visits.Add(MakeVisit(retainer, itemIds, rows));
            }
        }
        else if (rows.Any(r => !r.IsCrystal && r.Missing > 0))
        {
            var missingIds = rows.Where(r => !r.IsCrystal && r.Missing > 0).Select(r => r.ItemId).ToList();
            foreach (var retainer in retainers)
            {
                visits.Add(MakeVisit(retainer, missingIds, rows));
            }
        }

        Rows = rows;
        VisitPlans = visits;

        if (retainers.Count == 0)
        {
            Status = _retainers.IsReady
                ? "このキャラのリテイナーが見つかりません。"
                : "リテイナー情報を読めません。一度ベルで一覧を開いてください。";
            return false;
        }

        var missingCount = rows.Count(r => !r.IsCrystal && r.Missing > 0);
        if (missingCount == 0)
        {
            Status = $"{RecipeName} を {craftCount} 個分。不足している材料はありません。";
            return true;
        }

        if (visits.Count == 0)
        {
            Status = AllaganToolsAvailable
                ? $"{RecipeName} を {craftCount} 個分。不足はありますが、リテイナー在庫が見つかりません。"
                : $"{RecipeName} を {craftCount} 個分。Allagan Tools が無いので、一覧を開いて全リテイナーを確認します。";
            return !AllaganToolsAvailable;
        }

        var stockNote = AllaganToolsAvailable ? "Allagan Tools の在庫で計画しました。" : "Allagan Tools が無いので、全員を順に開きます。";
        Status = $"{RecipeName} を {craftCount} 個分。{visits.Count} 人のリテイナーから取り出します。{stockNote}";
        return true;
    }

    public bool StartWithdrawMissing()
    {
        if (IsRunning)
        {
            Status = "すでに取り出し中です。";
            return false;
        }

        if (Rows.Count == 0)
        {
            Status = "先に「レシピを読み取る」を押してください。";
            return false;
        }

        if (VisitPlans.Count == 0)
        {
            Status = "取り出す予定のリテイナーがありません。";
            return false;
        }

        if (!_ui.IsRetainerListOpen)
        {
            Status = "リテイナーベルで一覧（リテイナーリスト）を開いてから押してください。";
            return false;
        }

        _visits.Clear();
        foreach (var visit in VisitPlans)
        {
            visit.Status = "待ち";
            _visits.Enqueue(visit);
        }

        _withdrawItemIds.Clear();
        _currentVisit = null;
        _withdrawnCount = 0;
        _skippedCount = 0;
        _visitedCount = 0;
        SetPhase(Phase.SelectingRetainer, "リテイナーを順に開いて取り出します。");
        Subscribe();
        return true;
    }

    public void Cancel()
    {
        if (!IsRunning)
        {
            return;
        }

        _visits.Clear();
        _withdrawItemIds.Clear();
        _currentVisit = null;
        SetPhase(Phase.Idle, "取り出しを止めました。");
        Unsubscribe();
    }

    public void Dispose()
    {
        Cancel();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_phase == Phase.Idle)
        {
            Unsubscribe();
            return;
        }

        if (Environment.TickCount64 < _nextReadyTickMs)
        {
            return;
        }

        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "SmartRecipeRestock: multi-retainer tick threw.");
            Status = "エラーが起きたので止めました。";
            SetPhase(Phase.Idle, Status);
            Unsubscribe();
        }
    }

    private void Tick()
    {
        switch (_phase)
        {
            case Phase.SelectingRetainer:
                TickSelectRetainer();
                break;
            case Phase.WaitingSelectString:
                TickWait(Phase.OpeningTransfer, _ui.IsSelectStringOpen, "メニュー待ち…", "リテイナーを選べませんでした。次へ進みます。");
                break;
            case Phase.OpeningTransfer:
                if (_ui.TryOpenItemTransfer())
                {
                    SetPhase(Phase.WaitingTransfer, $"{CurrentName()} の受け渡し画面を開いています…");
                }
                else
                {
                    SkipCurrent("受け渡しを開けなかった");
                }

                break;
            case Phase.WaitingTransfer:
                TickWait(Phase.QueueWithdraws, _ui.IsTransferOpen || _inspector.IsTransferOpen, "受け渡し画面待ち…", "受け渡し画面が開きませんでした。次へ進みます。");
                break;
            case Phase.QueueWithdraws:
                QueueCurrentRetainerWithdraws();
                break;
            case Phase.Withdrawing:
                if (_queue.HasPending || _withdrawItemIds.Count > 0)
                {
                    TryQueueNextWithdraw();
                    return;
                }

                if (_currentVisit != null)
                {
                    _currentVisit.Status = "完了";
                }

                SetPhase(Phase.ClosingTransfer, $"{CurrentName()} から戻り、次のリテイナーへ…");
                break;
            case Phase.ClosingTransfer:
                _ui.TryCloseTransfer();
                SetPhase(Phase.WaitingSelectStringAfterClose, "一覧に戻っています…");
                break;
            case Phase.WaitingSelectStringAfterClose:
                if (_ui.IsRetainerListOpen)
                {
                    AdvanceToNextRetainer();
                    return;
                }

                if (_ui.IsSelectStringOpen)
                {
                    SetPhase(Phase.SelectingQuit, "話をやめて一覧に戻ります…");
                    return;
                }

                if (PhaseTimedOut())
                {
                    SkipCurrent("一覧に戻れなかった");
                }

                break;
            case Phase.SelectingQuit:
                _ui.TryQuitRetainer();
                SetPhase(Phase.WaitingRetainerList, "リテイナー一覧待ち…");
                break;
            case Phase.WaitingRetainerList:
                if (_ui.IsRetainerListOpen)
                {
                    AdvanceToNextRetainer();
                    return;
                }

                if (PhaseTimedOut())
                {
                    SkipCurrent("一覧に戻れなかった");
                }

                break;
        }
    }

    private void TickSelectRetainer()
    {
        if (_currentVisit == null)
        {
            if (!_visits.TryDequeue(out var next))
            {
                Finish();
                return;
            }

            _currentVisit = next;
        }

        if (!_ui.IsRetainerListOpen)
        {
            if (PhaseTimedOut())
            {
                SkipCurrent("リテイナー一覧が開いていない");
            }
            else
            {
                Status = "リテイナー一覧を開いたまま待っています…";
            }

            return;
        }

        if (_ui.TrySelectRetainer(_currentVisit.ListIndex))
        {
            _currentVisit.Status = "選択中";
            _visitedCount++;
            SetPhase(Phase.WaitingSelectString, $"{_currentVisit.Name} を開いています…");
            return;
        }

        SkipCurrent("選択に失敗");
    }

    private void TickWait(Phase next, bool ready, string waitingText, string timeoutText)
    {
        if (ready)
        {
            SetPhase(next, waitingText);
            return;
        }

        if (PhaseTimedOut())
        {
            SkipCurrent(timeoutText);
            return;
        }

        Status = waitingText;
    }

    private void QueueCurrentRetainerWithdraws()
    {
        _withdrawItemIds.Clear();
        var planned = _currentVisit?.ItemIds ?? [];
        foreach (var itemId in planned)
        {
            var row = Rows.FirstOrDefault(r => r.ItemId == itemId);
            if (row == null || row.IsCrystal)
            {
                continue;
            }

            var needed = row.AmountPerCraft * CraftCount;
            var playerCount = _playerInventory.Count(itemId);
            if (playerCount >= needed)
            {
                row.Status = "もう足りている";
                continue;
            }

            var live = _inspector.CountRetainerStock(itemId);
            if (live <= 0)
            {
                row.Status = $"{CurrentName()} に無い";
                continue;
            }

            _withdrawItemIds.Enqueue(itemId);
        }

        if (_withdrawItemIds.Count == 0)
        {
            if (_currentVisit != null)
            {
                _currentVisit.Status = "取り出す物なし";
            }

            SetPhase(Phase.ClosingTransfer, $"{CurrentName()} に不足材料が無かったので戻ります。");
            return;
        }

        if (_currentVisit != null)
        {
            _currentVisit.Status = $"取り出し中 ({_withdrawItemIds.Count})";
        }

        SetPhase(Phase.Withdrawing, $"{CurrentName()} から {_withdrawItemIds.Count} 種を取り出します…");
        TryQueueNextWithdraw();
    }

    private void TryQueueNextWithdraw()
    {
        if (_queue.HasPending || _withdrawItemIds.Count == 0)
        {
            return;
        }

        var itemId = _withdrawItemIds.Dequeue();
        var search = _inspector.FindRetainerItem(new RetainerItemSearchRequest
        {
            ItemId = itemId,
            Hq = null,
            MaxAmount = 0,
        });

        if (!search.Success || !search.CanIdentifyRow || search.InventorySlot == null
            || !RetainerInventoryInspector.TryParseInventoryType(search.InventoryContainer, out var inventoryType)
            || !_queue.QueueRetrieveCommand(inventoryType, search.InventorySlot.Value, WithdrawDelayMs, out _))
        {
            MarkRow(itemId, $"{CurrentName()} でスキップ");
            _skippedCount++;
            _nextReadyTickMs = Environment.TickCount64 + 300;
            return;
        }

        MarkRow(itemId, $"{CurrentName()} から取り出し予約");
        _withdrawnCount++;
        _nextReadyTickMs = Environment.TickCount64 + WithdrawDelayMs + 250;
        Status = $"{CurrentName()} 取り出し中… 予約 {_withdrawnCount} / 残り {_withdrawItemIds.Count}";
    }

    private void AdvanceToNextRetainer()
    {
        _currentVisit = null;
        _withdrawItemIds.Clear();
        if (_visits.Count == 0)
        {
            Finish();
            return;
        }

        SetPhase(Phase.SelectingRetainer, "次のリテイナーを開きます…");
    }

    private void SkipCurrent(string reason)
    {
        if (_currentVisit != null)
        {
            _currentVisit.Status = reason;
            _log.Warning("SmartRecipeRestock: skip retainer {Name}: {Reason}", _currentVisit.Name, reason);
        }

        _skippedCount++;
        _currentVisit = null;
        _withdrawItemIds.Clear();

        if (!_ui.IsRetainerListOpen && _ui.IsSelectStringOpen)
        {
            SetPhase(Phase.SelectingQuit, reason + " 一覧に戻します…");
            return;
        }

        if (_visits.Count == 0)
        {
            Finish();
            return;
        }

        SetPhase(Phase.SelectingRetainer, reason);
    }

    private void Finish()
    {
        SetPhase(
            Phase.Idle,
            $"完了。訪れたリテイナー {_visitedCount} 人 / 取り出し予約 {_withdrawnCount} / スキップ {_skippedCount}。");
        Unsubscribe();
    }

    private void SetPhase(Phase phase, string status)
    {
        _phase = phase;
        _phaseStartedTickMs = Environment.TickCount64;
        _nextReadyTickMs = Environment.TickCount64 + (_phase == Phase.Idle ? 0 : UiStepDelayMs);
        Status = status;
    }

    private bool PhaseTimedOut() => Environment.TickCount64 - _phaseStartedTickMs > WaitTimeoutMs;

    private string CurrentName() => _currentVisit?.Name ?? "リテイナー";

    private void MarkRow(uint itemId, string status)
    {
        var row = Rows.FirstOrDefault(r => r.ItemId == itemId);
        if (row != null)
        {
            row.Status = status;
        }
    }

    private static RetainerVisitPlan MakeVisit(KnownRetainer retainer, List<uint> itemIds, List<RestockMaterialRow> rows)
    {
        var names = itemIds
            .Select(id => rows.FirstOrDefault(r => r.ItemId == id)?.Name ?? id.ToString())
            .ToList();

        return new RetainerVisitPlan
        {
            RetainerId = retainer.RetainerId,
            Name = retainer.Name,
            ListIndex = retainer.ListIndex,
            ItemIds = [.. itemIds],
            ItemSummary = string.Join(", ", names),
            Status = "予定",
        };
    }

    private void ClearPlan()
    {
        RecipeId = 0;
        RecipeName = string.Empty;
        ResultItemId = 0;
        ResultItemName = string.Empty;
        Rows = [];
        VisitPlans = [];
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _framework.Update += OnFrameworkUpdate;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _framework.Update -= OnFrameworkUpdate;
        _subscribed = false;
    }

    private enum Phase
    {
        Idle,
        SelectingRetainer,
        WaitingSelectString,
        OpeningTransfer,
        WaitingTransfer,
        QueueWithdraws,
        Withdrawing,
        ClosingTransfer,
        WaitingSelectStringAfterClose,
        SelectingQuit,
        WaitingRetainerList,
    }
}
