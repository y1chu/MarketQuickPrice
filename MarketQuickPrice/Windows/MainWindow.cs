using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using MarketQuickPrice.Data;

namespace MarketQuickPrice.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly WorldPickerPopup worldPicker;
    private string itemSearch = string.Empty;
    private string? searchFeedback;
    private bool searchFeedbackIsError;
    private const int SearchInputMaxLength = 64;
    private string preferredWorld;
    private const int PreferredWorldMaxLength = 32;
    private const float PreferredWorldInputWidth = 220f;
    private const float PreferredWorldButtonWidth = 110f;
    private const float PreferredWorldPopupButtonWidth = 150f;
    private readonly string[] regionNames;
    private readonly bool[] customRegionSelection;
    private int cheapestScopeIndex = (int)LookupScopeKind.CurrentDataCenter;

    public MainWindow(Plugin plugin)
        : base("Market Quick Price##MQP_Main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(380, 160);
        SizeCondition = ImGuiCond.Appearing;
        this.plugin = plugin;
        worldPicker = new WorldPickerPopup();
        preferredWorld = plugin.Configuration.DefaultWorld ?? string.Empty;
        regionNames = WorldRegions.All.Select(r => r.Name).ToArray();
        customRegionSelection = new bool[regionNames.Length];
    }

    private LookupScope BuildCheapestLookupScope(IReadOnlyList<string> selectedRegions)
    {
        return (LookupScopeKind)cheapestScopeIndex switch
        {
            LookupScopeKind.CurrentDataCenter => LookupScope.CurrentDataCenter,
            LookupScopeKind.CurrentRegion => LookupScope.CurrentRegion,
            LookupScopeKind.CustomRegions => LookupScope.FromCustomRegions(selectedRegions),
            _ => LookupScope.SpecificWorld
        };
    }

    private List<string> GetSelectedCustomRegions()
    {
        var result = new List<string>();
        for (int i = 0; i < regionNames.Length; i++)
        {
            if (customRegionSelection[i])
                result.Add(regionNames[i]);
        }

        return result;
    }

    private string DescribeScope(LookupScope scope)
    {
        return scope.Kind switch
        {
            LookupScopeKind.CurrentDataCenter => "your current data center",
            LookupScopeKind.CurrentRegion => "your current region",
            LookupScopeKind.CustomRegions when scope.CustomRegions is { Count: > 0 }
                => $"regions: {string.Join(", ", scope.CustomRegions)}",
            _ => "your preferred world"
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped("Use the search box to look up an item. The /mqp command still works if you need it.");
        ImGui.Spacing();

        var triggered = false;
        ImGui.PushItemWidth(-110);
        var submitted = ImGui.InputText("##mqp_item_search", ref itemSearch, SearchInputMaxLength, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.PopItemWidth();
        triggered |= submitted;

        ImGui.SameLine();
        if (ImGui.Button("Search", new Vector2(100, 0)))
            triggered = true;

        if (triggered)
        {
            if (plugin.TryBeginLookup(itemSearch, out var error))
            {
                searchFeedbackIsError = false;
                searchFeedback = $"Searching for '{itemSearch.Trim()}'.";
            }
            else
            {
                searchFeedbackIsError = true;
                searchFeedback = error;
            }
        }

        if (!string.IsNullOrEmpty(searchFeedback))
        {
            var color = searchFeedbackIsError
                ? new Vector4(0.9f, 0.35f, 0.35f, 1f)
                : new Vector4(0.6f, 0.9f, 0.6f, 1f);
            ImGui.TextColored(color, searchFeedback);
        }

        ImGui.Spacing();
        DrawWorldPreferenceSection();
        ImGui.Spacing();
        DrawFindCheapestSection();
        ImGui.Spacing();
        ImGui.Separator();
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

    private void DrawWorldPreferenceSection()
    {
        ImGui.TextUnformatted("Preferred world");
        ImGui.SetNextItemWidth(PreferredWorldInputWidth);
        if (ImGui.InputText("##mqp_world_input", ref preferredWorld, PreferredWorldMaxLength))
        {
            UpdatePreferredWorld(preferredWorld);
        }

        ImGui.SameLine(0, 6);
        if (ImGui.Button("Use current", new Vector2(PreferredWorldButtonWidth, 0)))
        {
            var currentWorld = Plugin.ClientState.LocalPlayer is { } lp && lp.CurrentWorld.IsValid
                ? lp.CurrentWorld.Value.Name.ToString()
                : string.Empty;
            var normalized = currentWorld?.Trim() ?? string.Empty;
            UpdatePreferredWorld(string.Empty, string.IsNullOrEmpty(normalized) ? string.Empty : normalized);
        }

        ImGui.SameLine(0, 6);
        if (ImGui.Button("Choose from list", new Vector2(PreferredWorldPopupButtonWidth, 0)))
        {
            worldPicker.Open();
        }

        var storedWorld = plugin.Configuration.DefaultWorld ?? string.Empty;
        var current = string.IsNullOrWhiteSpace(storedWorld)
            ? "Using current character world"
            : $"Using: {storedWorld}";
        ImGui.TextUnformatted(current);

        worldPicker.Draw(preferredWorld, world => UpdatePreferredWorld(world));
    }

    private void DrawFindCheapestSection()
    {
        ImGui.TextUnformatted("Find cheapest in:");
        var currentScope = cheapestScopeIndex;
        if (ImGui.RadioButton("Current data center", ref currentScope, (int)LookupScopeKind.CurrentDataCenter))
            cheapestScopeIndex = currentScope;

        if (ImGui.RadioButton("Current region", ref currentScope, (int)LookupScopeKind.CurrentRegion))
            cheapestScopeIndex = currentScope;

        if (ImGui.RadioButton("Selected regions", ref currentScope, (int)LookupScopeKind.CustomRegions))
            cheapestScopeIndex = currentScope;

        if ((LookupScopeKind)cheapestScopeIndex == LookupScopeKind.CustomRegions)
        {
            ImGui.Indent();
            for (int i = 0; i < regionNames.Length; i++)
            {
                ImGui.Checkbox(regionNames[i], ref customRegionSelection[i]);
            }
            ImGui.Unindent();
        }

        var hasName = !string.IsNullOrWhiteSpace(itemSearch);
        var selectedRegions = GetSelectedCustomRegions();
        var scopeKind = (LookupScopeKind)cheapestScopeIndex;
        var selectionValid = scopeKind != LookupScopeKind.CustomRegions || selectedRegions.Count > 0;

        if (!hasName)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.35f, 0.35f, 1f), "Enter an item name above.");
        }
        else if (!selectionValid)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.35f, 0.35f, 1f), "Select at least one region.");
        }

        var canRun = hasName && selectionValid;
        if (!canRun) ImGui.BeginDisabled();

        if (ImGui.Button("Find cheapest", new Vector2(PreferredWorldPopupButtonWidth + 40, 0)))
        {
            var scope = BuildCheapestLookupScope(selectedRegions);
            if (plugin.TryBeginLookup(itemSearch, out var error, scope))
            {
                searchFeedbackIsError = false;
                searchFeedback = $"Searching '{itemSearch.Trim()}' in {DescribeScope(scope)}.";
            }
            else
            {
                searchFeedbackIsError = true;
                searchFeedback = error;
            }
        }

        if (!canRun) ImGui.EndDisabled();
    }

    private void UpdatePreferredWorld(string rawValue, string? displayOverride = null)
    {
        var normalized = rawValue.Trim();
        var displayValue = displayOverride ?? normalized;
        if (!string.Equals(preferredWorld, displayValue, StringComparison.Ordinal))
            preferredWorld = displayValue;

        if (string.Equals(plugin.Configuration.DefaultWorld ?? string.Empty, normalized, StringComparison.Ordinal))
            return;

        plugin.Configuration.DefaultWorld = normalized;
        plugin.Configuration.Save();
    }
}
