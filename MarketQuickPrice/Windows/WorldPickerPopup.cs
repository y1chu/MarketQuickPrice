using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using MarketQuickPrice.Data;

namespace MarketQuickPrice.Windows;

public sealed class WorldPickerPopup
{
    private readonly IReadOnlyList<RegionInfo> regions = WorldRegions.All;
    private readonly string popupId;

    private const int WorldColumns = 3;
    private const float ButtonWidth = 120f;
    private const float ButtonSpacing = 6f;

    private bool isOpen;
    private Vector2 windowSize = new(480, 380);
    private static readonly Vector2 MinSize = new(360, 260);
    private static readonly Vector2 MaxSize = new(900, 700);

    public WorldPickerPopup(string popupId = "MQP_WorldPicker")
    {
        this.popupId = popupId;
    }

    public void Open() => isOpen = true;

    public void Draw(string selectedWorld, Action<string> onWorldChosen)
    {
        if (!isOpen)
            return;

        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(MinSize, MaxSize);

        var title = $"World Picker##{popupId}";
        if (!ImGui.Begin(title, ref isOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
        {
            ImGui.End();
            return;
        }

        windowSize = ImGui.GetWindowSize();

        ImGui.TextUnformatted("Select a world");
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
                    DrawWorldButtons(dataCenter.Worlds, selectedWorld, onWorldChosen);
                    ImGui.Unindent();
                    ImGui.Spacing();
                    ImGui.PopID();
                }
            }
        }
        ImGui.EndChild();

        if (ImGui.Button("Clear selection"))
        {
            onWorldChosen(string.Empty);
            isOpen = false;
        }

        ImGui.SameLine();

        if (ImGui.Button("Close"))
            isOpen = false;

        ImGui.End();
    }

    private void DrawWorldButtons(IReadOnlyList<string> worlds, string selectedWorld, Action<string> onWorldChosen)
    {
        for (int i = 0; i < worlds.Count; i++)
        {
            if (i % WorldColumns != 0)
                ImGui.SameLine(0, ButtonSpacing);

            var worldName = worlds[i];
            var isSelected = !string.IsNullOrWhiteSpace(selectedWorld) &&
                string.Equals(worldName, selectedWorld, StringComparison.OrdinalIgnoreCase);

            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.33f, 0.6f, 0.33f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.38f, 0.7f, 0.38f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.28f, 0.55f, 0.28f, 1f));
            }

            if (ImGui.Button(worldName, new Vector2(ButtonWidth, 0)))
            {
                onWorldChosen(worldName);
                isOpen = false;
            }

            if (isSelected)
                ImGui.PopStyleColor(3);
        }
    }
}
