using FFXIVClientStructs.FFXIV.Client.Game;
using SmartRecipeRestockHelper.Models;

namespace SmartRecipeRestockHelper.Services;

public sealed unsafe class RetainerDirectory
{
    public bool IsReady
    {
        get
        {
            var manager = RetainerManager.Instance();
            return manager != null && manager->IsReady;
        }
    }

    public List<KnownRetainer> GetCurrentCharacterRetainers()
    {
        var result = new List<KnownRetainer>();
        var manager = RetainerManager.Instance();
        if (manager == null || !manager->IsReady)
        {
            return result;
        }

        var count = manager->GetRetainerCount();
        for (uint i = 0; i < count; i++)
        {
            var retainer = manager->GetRetainerBySortedIndex(i);
            if (retainer == null || retainer->RetainerId == 0)
            {
                continue;
            }

            var name = retainer->NameString;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            result.Add(new KnownRetainer
            {
                RetainerId = retainer->RetainerId,
                Name = name,
                ListIndex = (int)i,
            });
        }

        return result;
    }
}
