using Dalamud.Configuration;
using System;

namespace MarketQuickPrice;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public string DefaultWorld { get; set; } = "";
    public int MinSecondsBetweenCalls { get; set; } = 2;
    public int HistoryCapacity { get; set; } = 5;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
