using System.Collections.Generic;

namespace MarketQuickPrice;

internal enum LookupScopeKind
{
    SpecificWorld,
    CurrentDataCenter,
    CurrentRegion,
    CustomRegions,
}

internal readonly record struct LookupScope(LookupScopeKind Kind, IReadOnlyList<string>? CustomRegions = null)
{
    public static LookupScope SpecificWorld { get; } = new(LookupScopeKind.SpecificWorld);

    public static LookupScope CurrentDataCenter { get; } = new(LookupScopeKind.CurrentDataCenter);

    public static LookupScope CurrentRegion { get; } = new(LookupScopeKind.CurrentRegion);

    public static LookupScope FromCustomRegions(IReadOnlyList<string> regions)
        => new(LookupScopeKind.CustomRegions, regions);
}
