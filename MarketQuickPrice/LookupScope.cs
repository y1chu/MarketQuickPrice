using System.Collections.Generic;

namespace MarketQuickPrice;

internal enum LookupScopeKind
{
    SpecificWorld,
    CurrentDataCenter,
    CurrentRegion,
    CustomRegions,
    CustomWorlds,
}

internal readonly record struct LookupScope(
    LookupScopeKind Kind,
    IReadOnlyList<string>? CustomRegions = null,
    IReadOnlyList<string>? CustomWorlds = null)
{
    public static LookupScope SpecificWorld { get; } = new(LookupScopeKind.SpecificWorld);

    public static LookupScope CurrentDataCenter { get; } = new(LookupScopeKind.CurrentDataCenter);

    public static LookupScope CurrentRegion { get; } = new(LookupScopeKind.CurrentRegion);

    public static LookupScope FromCustomRegions(IReadOnlyList<string> regions)
        => new(LookupScopeKind.CustomRegions, regions);

    public static LookupScope FromCustomWorlds(IReadOnlyList<string> worlds)
        => new(LookupScopeKind.CustomWorlds, null, worlds);
}
