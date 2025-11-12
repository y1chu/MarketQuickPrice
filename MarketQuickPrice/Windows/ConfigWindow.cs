using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace MarketQuickPrice.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("Market Quick Price — Settings###MQP_Config")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(420, 160);
        SizeCondition = ImGuiCond.Appearing;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped("Preferred world selection now lives in the main window search panel.");
        ImGui.Spacing();

        var cap = configuration.HistoryCapacity;
        if (ImGui.SliderInt("Hisory entries", ref cap, 5, 30))
        {
            configuration.HistoryCapacity = Math.Clamp(cap, 5, 30);
            configuration.Save();
        }
    }
}
