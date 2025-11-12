using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

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
        // Default World
        var worldBuf = configuration.DefaultWorld ?? string.Empty;
        if (ImGui.InputText("Price Check World (blank = current)", ref worldBuf, 32))
        {
            configuration.DefaultWorld = worldBuf.Trim();
            configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            configuration.DefaultWorld = string.Empty;
            configuration.Save();
        }

        ImGui.Spacing();

        // Throttle
        var throttle = configuration.MinSecondsBetweenCalls;
        if (ImGui.SliderInt("Min seconds between calls", ref throttle, 1, 30))
        {
            configuration.MinSecondsBetweenCalls = Math.Clamp(throttle, 1, 30);
            configuration.Save();
        }

        ImGui.Spacing();

        var cap = configuration.HistoryCapacity;
        if (ImGui.SliderInt("Hisory entries", ref cap, 5, 30))
        {
            configuration.HistoryCapacity = Math.Clamp(cap, 5, 30);
            configuration.Save();
        }
    }
}
