using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

public sealed class RetainerContextValidator
{
    private readonly ICondition _condition;
    private readonly RetainerInventoryInspector _inspector;

    public RetainerContextValidator(ICondition condition, RetainerInventoryInspector inspector)
    {
        _condition = condition;
        _inspector = inspector;
    }

    public SmartRestockStatus Validate()
    {
        var addonName = _inspector.DetectTransferAddonName();
        var transferOpen = !string.IsNullOrEmpty(addonName);
        var bellOccupied = _condition[ConditionFlag.OccupiedSummoningBell];
        var retainerAgentActive = IsRetainerAgentActive();

        var valid = bellOccupied && (transferOpen || retainerAgentActive);
        var message = valid
            ? "Retainer context looks valid for read-only inspection."
            : "Retainer context is not ready (bell not occupied or transfer screen not detected).";

        return new SmartRestockStatus
        {
            Phase = "4A",
            ReadOnly = true,
            WithdrawalEnabled = false,
            ItemSelectionEnabled = false,
            DetectedAddonName = addonName ?? string.Empty,
            TransferScreenOpen = transferOpen,
            RetainerContextValid = valid,
            Message = message,
        };
    }

    private static unsafe bool IsRetainerAgentActive()
    {
        var agentModule = AgentModule.Instance();
        if (agentModule == null)
        {
            return false;
        }

        var agent = agentModule->GetAgentByInternalId(AgentId.Retainer);
        return agent != null && agent->IsAgentActive();
    }
}
