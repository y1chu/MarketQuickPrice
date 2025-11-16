using Dalamud.Configuration;
using System;
using System.Numerics;

namespace MarketQuickPrice;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public string DefaultWorld { get; set; } = "";
    public int HistoryCapacity { get; set; } = 5;
    public Vector2 MainWindowSize { get; set; } = new(380, 160);
    public Vector2 ConfigWindowSize { get; set; } = new(420, 160);
    public Vector2 WorldPickerSize { get; set; } = new(480, 380);
    public string[] PreferredWorldList { get; set; } = Array.Empty<string>();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
