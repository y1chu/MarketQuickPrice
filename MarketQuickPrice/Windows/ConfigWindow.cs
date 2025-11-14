using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace MarketQuickPrice.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private Vector2 trackedWindowSize;
    private bool sizeDirty;
    private DateTime sizeDirtySince;
    private const float SizeChangeThreshold = 0.5f;
    private static readonly TimeSpan SizeSaveDelay = TimeSpan.FromMilliseconds(400);

    public ConfigWindow(Plugin plugin) : base("Market Quick Price — Settings###MQP_Config")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        configuration = plugin.Configuration;
        var initialSize = ResolveWindowSize(configuration.ConfigWindowSize, new Vector2(420, 160));
        Size = initialSize;
        trackedWindowSize = initialSize;
        SizeCondition = ImGuiCond.Appearing;
    }

    public void Dispose()
        => SaveWindowSizeIfNeeded(true);

    public override void OnClose()
    {
        SaveWindowSizeIfNeeded(true);
        base.OnClose();
    }

    public override void Draw()
    {
        TrackWindowSizeChange();
        SaveWindowSizeIfNeeded();

        ImGui.TextWrapped("Preferred world selection now lives in the main window search panel.");
        ImGui.Spacing();

        var cap = configuration.HistoryCapacity;
        if (ImGui.SliderInt("Hisory entries", ref cap, 5, 30))
        {
            configuration.HistoryCapacity = Math.Clamp(cap, 5, 30);
            configuration.Save();
        }
    }

    private static Vector2 ResolveWindowSize(Vector2 stored, Vector2 fallback)
        => stored.X > 0 && stored.Y > 0 ? stored : fallback;

    private void TrackWindowSizeChange()
    {
        var currentSize = ImGui.GetWindowSize();
        if (Vector2.DistanceSquared(currentSize, trackedWindowSize) <= SizeChangeThreshold * SizeChangeThreshold)
            return;

        trackedWindowSize = currentSize;
        Size = currentSize;
        configuration.ConfigWindowSize = currentSize;
        sizeDirtySince = DateTime.UtcNow;
        sizeDirty = true;
    }

    private void SaveWindowSizeIfNeeded(bool force = false)
    {
        if (!sizeDirty)
            return;

        if (!force && (DateTime.UtcNow - sizeDirtySince) < SizeSaveDelay)
            return;

        configuration.Save();
        sizeDirty = false;
    }
}
