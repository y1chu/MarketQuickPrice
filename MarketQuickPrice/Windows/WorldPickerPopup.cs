using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using MarketQuickPrice.Data;

namespace MarketQuickPrice.Windows;

public sealed class WorldPickerPopup
{
    private readonly Configuration configuration;
    private readonly IReadOnlyList<RegionInfo> regions = WorldRegions.All;
    private readonly string popupId;

    private const int WorldColumns = 3;
    private const float ButtonWidth = 140f;
    private const float ButtonSpacing = 6f;

    private bool isOpen;
    private Vector2 windowSize = new(480, 380);
    private static readonly Vector2 MinSize = new(360, 260);
    private static readonly Vector2 MaxSize = new(900, 700);
    private Vector2 trackedWindowSize;
    private bool sizeDirty;
    private DateTime sizeDirtySince;
    private const float SizeChangeThreshold = 0.5f;
    private static readonly TimeSpan SizeSaveDelay = TimeSpan.FromMilliseconds(400);

    public WorldPickerPopup(Configuration configuration, string popupId = "MQP_WorldPicker")
    {
        this.configuration = configuration;
        this.popupId = popupId;
        windowSize = ResolveWindowSize(configuration.WorldPickerSize, windowSize);
        trackedWindowSize = windowSize;
    }

    private static Vector2 ResolveWindowSize(Vector2 stored, Vector2 fallback)
        => stored.X > 0 && stored.Y > 0 ? stored : fallback;

    private string currentPreferredWorld = string.Empty;
    private readonly List<string> multiSelection = new();
    private readonly HashSet<string> multiSelectionLookup = new(StringComparer.OrdinalIgnoreCase);
    private Action<string>? onPreferredWorldChosen;
    private Action<IReadOnlyList<string>>? onMultiSelectionApplied;
    private bool selectionDirty;

    public void Open(string preferredWorld, IReadOnlyList<string> selectedWorlds, Action<string> onPreferredWorldChosen, Action<IReadOnlyList<string>> onMultiSelectionApplied)
    {
        currentPreferredWorld = preferredWorld?.Trim() ?? string.Empty;
        this.onPreferredWorldChosen = onPreferredWorldChosen;
        this.onMultiSelectionApplied = onMultiSelectionApplied;

        multiSelection.Clear();
        multiSelectionLookup.Clear();
        if (selectedWorlds is { Count: > 0 })
        {
            foreach (var world in selectedWorlds)
            {
                var normalized = world?.Trim();
                if (string.IsNullOrEmpty(normalized))
                    continue;

                if (multiSelectionLookup.Add(normalized))
                    multiSelection.Add(normalized);
            }
        }

        selectionDirty = false;
        isOpen = true;
    }

    public void Draw()
    {
        if (!isOpen)
            return;

        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(MinSize, MaxSize);

        var title = $"World Picker##{popupId}";
        if (!ImGui.Begin(title, ref isOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
        {
            ImGui.End();
            SaveWindowSizeIfNeeded(!isOpen);
            return;
        }

        windowSize = ImGui.GetWindowSize();
        TrackWindowSizeChange();

        ImGui.TextWrapped("Click a world name to set your preferred world. Use the checkboxes to include worlds when searching.");
        ImGui.Separator();

        var childHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing() * 2;
        if (childHeight < 150f)
            childHeight = 150f;

        if (ImGui.BeginChild("##mqp_world_selector", new Vector2(-1, childHeight), true))
        {
            foreach (var region in regions)
            {
                var headerOpen = ImGui.CollapsingHeader($"{region.Name}##mqp_region_{region.Name}", ImGuiTreeNodeFlags.DefaultOpen);
                if (!headerOpen)
                    continue;

                foreach (var dataCenter in region.DataCenters)
                {
                    ImGui.PushID($"{region.Name}_{dataCenter.Name}");
                    ImGui.TextColored(new Vector4(0.75f, 0.85f, 1f, 1f), $"{dataCenter.Name} Data Center");
                    ImGui.Indent();
                    DrawWorldButtons(dataCenter.Worlds);
                    ImGui.Unindent();
                    ImGui.Spacing();
                    ImGui.PopID();
                }
            }
        }
        ImGui.EndChild();

        if (ImGui.Button("Apply selection"))
            ApplyMultiSelection(true);

        ImGui.SameLine();
        if (ImGui.Button("Close"))
        {
            ApplyMultiSelection(true);
            isOpen = false;
        }

        ImGui.End();
        SaveWindowSizeIfNeeded(!isOpen);

        if (!isOpen && selectionDirty)
            ApplyMultiSelection(true);
    }

    private void DrawWorldButtons(IReadOnlyList<string> worlds)
    {
        for (int i = 0; i < worlds.Count; i++)
        {
            if (i % WorldColumns != 0)
                ImGui.SameLine(0, ButtonSpacing);

            var worldName = worlds[i];
            ImGui.PushID(worldName);
            ImGui.BeginGroup();

            var included = multiSelectionLookup.Contains(worldName);
            if (ImGui.Checkbox("##mqp_world_select", ref included))
                ToggleSelection(worldName, included);

            var label = string.Equals(worldName, currentPreferredWorld, StringComparison.OrdinalIgnoreCase)
                ? $"{worldName} (preferred)"
                : worldName;

            if (ImGui.Button(label, new Vector2(ButtonWidth, 0)))
            {
                currentPreferredWorld = worldName;
                onPreferredWorldChosen?.Invoke(worldName);
            }

            ImGui.EndGroup();
            ImGui.PopID();
        }
    }

    private void ToggleSelection(string worldName, bool include)
    {
        if (include)
        {
            if (multiSelectionLookup.Add(worldName))
                multiSelection.Add(worldName);
        }
        else
        {
            if (multiSelectionLookup.Remove(worldName))
                multiSelection.RemoveAll(w => string.Equals(w, worldName, StringComparison.OrdinalIgnoreCase));
        }

        selectionDirty = true;
    }

    private void ApplyMultiSelection(bool force = false)
    {
        if ((!force && !selectionDirty) || onMultiSelectionApplied is null)
            return;

        onMultiSelectionApplied(multiSelection.ToList());
        selectionDirty = false;
    }

    private void TrackWindowSizeChange()
    {
        if (Vector2.DistanceSquared(windowSize, trackedWindowSize) <= SizeChangeThreshold * SizeChangeThreshold)
            return;

        trackedWindowSize = windowSize;
        configuration.WorldPickerSize = windowSize;
        sizeDirty = true;
        sizeDirtySince = DateTime.UtcNow;
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
