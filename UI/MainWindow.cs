using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SmartRecipeRestockHelper.Services;

namespace SmartRecipeRestockHelper.UI;

public sealed class MainWindow : Window
{
    private readonly StandaloneRestockService _restock;
    private readonly RetainerInventoryInspector _inspector;

    private int _craftCount = 1;
    private bool _allowFullStack = true;

    public MainWindow(StandaloneRestockService restock, RetainerInventoryInspector inspector)
        : base("Smart Recipe Restock")
    {
        _restock = restock;
        _inspector = inspector;

        Size = new Vector2(820, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.TextWrapped("開いているレシピの不足材料を、各リテイナーから順に取り出します。SND は使いません。");
        ImGui.TextWrapped("注意: ゲームの仕様で、1スタック全部出ます。必要な数だけ、ではありません。");
        ImGui.Separator();

        ImGui.TextUnformatted("1. 製作ノートを開いてレシピを選ぶ");
        ImGui.TextUnformatted("2. 「レシピを読み取る」を押す");
        ImGui.TextUnformatted("3. リテイナーベルで一覧を開く（受け渡し画面までは開かなくてよい）");
        ImGui.TextUnformatted("4. 「全リテイナーから取り出す」を押す");
        ImGui.Separator();

        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("製作数", ref _craftCount))
        {
            _craftCount = Math.Clamp(_craftCount, 1, 999);
        }

        ImGui.SameLine();
        ImGui.Checkbox("スタックごと取り出してよい", ref _allowFullStack);

        var busy = _restock.IsRunning;
        if (busy)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("レシピを読み取る"))
        {
            _restock.RefreshPlan(_craftCount);
        }

        ImGui.SameLine();
        var canWithdraw = _allowFullStack && _restock.VisitPlans.Count > 0;
        if (!canWithdraw && !busy)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("全リテイナーから取り出す") && canWithdraw)
        {
            _restock.StartWithdrawMissing();
        }

        if (!canWithdraw && !busy)
        {
            ImGui.EndDisabled();
        }

        if (busy)
        {
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("中止"))
            {
                _restock.Cancel();
            }
        }

        ImGui.Spacing();
        ImGui.TextWrapped(_restock.Status);

        var recipeLabel = _restock.RecipeId == 0
            ? "レシピ: 未選択"
            : $"レシピ: {_restock.RecipeName}  (ID {_restock.RecipeId})";
        ImGui.TextUnformatted(recipeLabel);

        var stockSource = _restock.AllaganToolsAvailable
            ? "在庫参照: Allagan Tools"
            : "在庫参照: なし（全員を順に開いて確認）";
        var list = _inspector.IsAtRetainerBell ? "ベル前: はい" : "ベル前: いいえ";
        ImGui.TextUnformatted($"{list}   {stockSource}");

        if (_restock.VisitPlans.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("取り出し予定のリテイナー");
            if (ImGui.BeginTable("srr-visits", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg, new Vector2(0, 140)))
            {
                ImGui.TableSetupColumn("リテイナー", ImGuiTableColumnFlags.WidthFixed, 160);
                ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("状態", ImGuiTableColumnFlags.WidthFixed, 180);
                ImGui.TableHeadersRow();

                foreach (var visit in _restock.VisitPlans)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(visit.Name);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(visit.ItemSummary);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(visit.Status);
                }

                ImGui.EndTable();
            }
        }

        if (_restock.Rows.Count == 0)
        {
            return;
        }

        ImGui.Separator();
        if (ImGui.BeginTable("srr-materials", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 240)))
        {
            ImGui.TableSetupColumn("材料", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("必要", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("所持", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("不足", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("どのリテイナー", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("状態", ImGuiTableColumnFlags.WidthFixed, 160);
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            foreach (var row in _restock.Rows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Name);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Needed.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.PlayerCount.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Missing.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.SourceRetainers);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Status);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.ItemId.ToString());
            }

            ImGui.EndTable();
        }
    }
}
