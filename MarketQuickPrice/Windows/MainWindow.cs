using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MarketQuickPrice.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Market Quick Price##MQP_Main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(380, 160);
        SizeCondition = ImGuiCond.Appearing;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextUnformatted("Type /mqp <item name> in chat to query.");
        ImGui.Spacing();

        if (plugin.History.Count > 0)
        {
            if (ImGui.BeginTable("mqp_hist", 4,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,
                new Vector2(-1, 140)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("When", 0, 0.35f);
                ImGui.TableSetupColumn("Item", 0, 0.35f);
                ImGui.TableSetupColumn("World", 0, 0.15f);
                ImGui.TableSetupColumn("Lowest", 0, 0.15f);
                ImGui.TableHeadersRow();

                for (int i = 0; i < plugin.History.Count; i++)
                {
                    var h = plugin.History[i];
                    ImGui.TableNextRow();

                    // make whole row clickable
                    ImGui.TableNextColumn();
                    var when = DateTimeOffset.FromUnixTimeMilliseconds(h.LastUploadMs).LocalDateTime;
                    if (ImGui.Selectable($"{when:g}##when{i}", false, ImGuiSelectableFlags.SpanAllColumns))
                        plugin.LastResult = h;

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(h.ItemName);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(h.World);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{h.Lowest:n0}");
                }

                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextUnformatted("No history yet.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // CURRENT RESULT (bottom)
        var r = plugin.LastResult;
        if (r is null)
        {
            ImGui.TextUnformatted("No current result.");
            if (ImGui.Button("Settings")) plugin.ToggleConfigUi();
            return;
        }

        ImGui.TextUnformatted("Current result");
        ImGui.Separator();
        ImGui.TextUnformatted($"{r.ItemName} — {r.World}");
        ImGui.TextUnformatted($"Lowest: {r.Lowest:n0} gil");
        var now = DateTimeOffset.FromUnixTimeMilliseconds(r.LastUploadMs).LocalDateTime;
        ImGui.TextUnformatted($"Last updated: {now:g}");

        ImGui.Spacing();
        if (ImGui.Button("Settings")) plugin.ToggleConfigUi();
    }
}
