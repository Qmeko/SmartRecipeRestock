using FFXIVClientStructs.FFXIV.Client.Game;

namespace SmartRecipeRestockHelper.Services;

public sealed unsafe class PlayerInventoryCounter
{
    private static readonly InventoryType[] PlayerBags =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.Crystals,
    ];

    public int Count(uint itemId)
    {
        if (itemId == 0)
        {
            return 0;
        }

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            return 0;
        }

        var total = 0;
        foreach (var containerType in PlayerBags)
        {
            var container = inventoryManager->GetInventoryContainer(containerType);
            if (container == null)
            {
                continue;
            }

            for (var slotIndex = 0; slotIndex < container->Size; slotIndex++)
            {
                var slot = container->GetInventorySlot(slotIndex);
                if (slot == null || slot->ItemId != itemId || slot->Quantity <= 0)
                {
                    continue;
                }

                total += slot->Quantity;
            }
        }

        return total;
    }
}
