using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SmartRecipeRestockHelper.Services;

/// <summary>Minimal read-only number-array reader (ECommons AtkReader subset).</summary>
public unsafe class AtkAddonReader
{
    private readonly NumberArrayData* _numberArray;
    protected int BeginOffset;

    protected AtkAddonReader(NumberArrayData* numberArray, int beginOffset = 0)
    {
        _numberArray = numberArray;
        BeginOffset = beginOffset;
    }

    protected uint? ReadUInt(int relativeOffset)
    {
        if (_numberArray == null)
        {
            return null;
        }

        var index = BeginOffset + relativeOffset;
        if (index < 0 || index >= _numberArray->Size)
        {
            return null;
        }

        return (uint)_numberArray->IntArray[index];
    }
}
