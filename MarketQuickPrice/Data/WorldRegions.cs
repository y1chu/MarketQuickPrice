using System.Collections.Generic;

namespace MarketQuickPrice.Data;

internal sealed record DataCenterInfo(string Name, IReadOnlyList<string> Worlds);
internal sealed record RegionInfo(string Name, IReadOnlyList<DataCenterInfo> DataCenters);

internal static class WorldRegions
{
    public static IReadOnlyList<RegionInfo> All { get; } = new[]
    {
        new RegionInfo("North America", new[]
        {
            new DataCenterInfo("Aether", new[]
            {
                "Adamantoise", "Cactuar", "Faerie", "Gilgamesh",
                "Jenova", "Midgardsormr", "Sargatanas", "Siren"
            }),
            new DataCenterInfo("Crystal", new[]
            {
                "Balmung", "Brynhildr", "Coeurl", "Diabolos",
                "Goblin", "Malboro", "Mateus", "Zalera"
            }),
            new DataCenterInfo("Primal", new[]
            {
                "Behemoth", "Excalibur", "Exodus", "Famfrit",
                "Hyperion", "Lamia", "Leviathan", "Ultros"
            }),
            new DataCenterInfo("Dynamis", new[]
            {
                "Halicarnassus", "Maduin", "Marilith", "Seraph",
                "Cuchulainn", "Golem", "Kraken", "Rafflesia"
            }),
        }),
        new RegionInfo("Europe", new[]
        {
            new DataCenterInfo("Chaos", new[]
            {
                "Cerberus", "Louisoix", "Moogle", "Omega",
                "Phantom", "Ragnarok", "Sagittarius", "Spriggan"
            }),
            new DataCenterInfo("Light", new[]
            {
                "Alpha", "Lich", "Odin", "Phoenix",
                "Raiden", "Shiva", "Twintania", "Zodiark"
            }),
        }),
        new RegionInfo("Japan", new[]
        {
            new DataCenterInfo("Elemental", new[]
            {
                "Aegis", "Atomos", "Carbuncle", "Garuda",
                "Gungnir", "Kujata", "Tonberry", "Typhon"
            }),
            new DataCenterInfo("Gaia", new[]
            {
                "Alexander", "Bahamut", "Durandal", "Fenrir",
                "Ifrit", "Ridill", "Tiamat", "Ultima"
            }),
            new DataCenterInfo("Mana", new[]
            {
                "Anima", "Asura", "Chocobo", "Hades",
                "Ixion", "Masamune", "Pandaemonium", "Titan"
            }),
            new DataCenterInfo("Meteor", new[]
            {
                "Belias", "Mandragora", "Ramuh", "Shinryu",
                "Unicorn", "Valefor", "Yojimbo", "Zeromus"
            }),
        }),
        new RegionInfo("Oceania", new[]
        {
            new DataCenterInfo("Materia", new[]
            {
                "Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan"
            }),
        }),
    };
}
