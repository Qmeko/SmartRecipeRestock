namespace SmartRecipeRestockHelper.Models;

public sealed class KnownRetainer
{
    public ulong RetainerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int ListIndex { get; init; }
}
