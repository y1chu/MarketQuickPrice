using System;
using System.Collections.Generic;

namespace MarketQuickPrice.Data;

internal sealed record DataCenterInfo(string Name, string ApiIdentifier, IReadOnlyList<string> Worlds);
internal sealed record RegionInfo(string Name, string ApiIdentifier, IReadOnlyList<DataCenterInfo> DataCenters);

internal static class WorldRegions
{
    public static IReadOnlyList<RegionInfo> All { get; }

    private static readonly Dictionary<string, (RegionInfo Region, DataCenterInfo DataCenter)> WorldLookup;
    private static readonly Dictionary<string, RegionInfo> RegionLookup;

    static WorldRegions()
    {
        All = new[]
        {
            new RegionInfo("North America", "North-America", new[]
            {
                new DataCenterInfo("Aether", "Aether", new[]
                {
                    "Adamantoise", "Cactuar", "Faerie", "Gilgamesh",
                    "Jenova", "Midgardsormr", "Sargatanas", "Siren"
                }),
                new DataCenterInfo("Crystal", "Crystal", new[]
                {
                    "Balmung", "Brynhildr", "Coeurl", "Diabolos",
                    "Goblin", "Malboro", "Mateus", "Zalera"
                }),
                new DataCenterInfo("Primal", "Primal", new[]
                {
                    "Behemoth", "Excalibur", "Exodus", "Famfrit",
                    "Hyperion", "Lamia", "Leviathan", "Ultros"
                }),
                new DataCenterInfo("Dynamis", "Dynamis", new[]
                {
                    "Halicarnassus", "Maduin", "Marilith", "Seraph",
                    "Cuchulainn", "Golem", "Kraken", "Rafflesia"
                }),
            }),
            new RegionInfo("Europe", "Europe", new[]
            {
                new DataCenterInfo("Chaos", "Chaos", new[]
                {
                    "Cerberus", "Louisoix", "Moogle", "Omega",
                    "Phantom", "Ragnarok", "Sagittarius", "Spriggan"
                }),
                new DataCenterInfo("Light", "Light", new[]
                {
                    "Alpha", "Lich", "Odin", "Phoenix",
                    "Raiden", "Shiva", "Twintania", "Zodiark"
                }),
            }),
            new RegionInfo("Japan", "Japan", new[]
            {
                new DataCenterInfo("Elemental", "Elemental", new[]
                {
                    "Aegis", "Atomos", "Carbuncle", "Garuda",
                    "Gungnir", "Kujata", "Tonberry", "Typhon"
                }),
                new DataCenterInfo("Gaia", "Gaia", new[]
                {
                    "Alexander", "Bahamut", "Durandal", "Fenrir",
                    "Ifrit", "Ridill", "Tiamat", "Ultima"
                }),
                new DataCenterInfo("Mana", "Mana", new[]
                {
                    "Anima", "Asura", "Chocobo", "Hades",
                    "Ixion", "Masamune", "Pandaemonium", "Titan"
                }),
                new DataCenterInfo("Meteor", "Meteor", new[]
                {
                    "Belias", "Mandragora", "Ramuh", "Shinryu",
                    "Unicorn", "Valefor", "Yojimbo", "Zeromus"
                }),
            }),
            new RegionInfo("Oceania", "Oceania", new[]
            {
                new DataCenterInfo("Materia", "Materia", new[]
                {
                    "Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan"
                }),
            }),
        };

        WorldLookup = new Dictionary<string, (RegionInfo, DataCenterInfo)>(StringComparer.OrdinalIgnoreCase);
        RegionLookup = new Dictionary<string, RegionInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var region in All)
        {
            RegionLookup[region.Name] = region;
            foreach (var dataCenter in region.DataCenters)
            {
                foreach (var world in dataCenter.Worlds)
                {
                    if (!WorldLookup.ContainsKey(world))
                        WorldLookup[world] = (region, dataCenter);
                }
            }
        }
    }

    public static bool TryGetWorldInfo(string worldName, out RegionInfo region, out DataCenterInfo dataCenter)
    {
        if (WorldLookup.TryGetValue(worldName, out var pair))
        {
            region = pair.Region;
            dataCenter = pair.DataCenter;
            return true;
        }

        region = null!;
        dataCenter = null!;
        return false;
    }

    public static bool TryGetRegionByName(string regionName, out RegionInfo region)
    {
        var found = RegionLookup.TryGetValue(regionName, out var info);
        region = info!;
        return found;
    }
}
