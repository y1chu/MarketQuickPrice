using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
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
    private readonly List<string> preferredWorldSelections;
    private const int PreferredWorldMaxLength = 32;
    private const float PreferredWorldInputWidth = 220f;
    private const float PreferredWorldButtonWidth = 110f;
    private const float PreferredWorldPopupButtonWidth = 150f;
    private const float FindCheapestButtonWidth = PreferredWorldPopupButtonWidth + 40f;
    private const float ResultIconSize = 72f;
    private readonly string[] regionNames;
    private readonly bool[] customRegionSelection;
    private int cheapestScopeIndex = (int)LookupScopeKind.CurrentDataCenter;
    private Vector2 trackedWindowSize;
    private bool sizeDirty;
    private DateTime sizeDirtySince;
    private const float SizeChangeThreshold = 0.5f;
    private static readonly TimeSpan SizeSaveDelay = TimeSpan.FromMilliseconds(400);

    public MainWindow(Plugin plugin)
        : base("Market Quick Price##MQP_Main")
    {
        var initialSize = ResolveWindowSize(plugin.Configuration.MainWindowSize, new Vector2(380, 160));
        Size = initialSize;
        SizeCondition = ImGuiCond.Appearing;
        trackedWindowSize = initialSize;
        this.plugin = plugin;
        worldPicker = new WorldPickerPopup(plugin.Configuration);
        preferredWorld = plugin.Configuration.DefaultWorld ?? string.Empty;
        preferredWorldSelections = new List<string>(plugin.Configuration.PreferredWorldList ?? Array.Empty<string>());
        NormalizePreferredWorldSelections();
        regionNames = WorldRegions.All.Select(r => r.Name).ToArray();
        customRegionSelection = new bool[regionNames.Length];
    }

    private static Vector2 ResolveWindowSize(Vector2 storedSize, Vector2 fallback)
        => storedSize.X > 0 && storedSize.Y > 0 ? storedSize : fallback;

    private LookupScope BuildCheapestLookupScope(IReadOnlyList<string> selectedRegions)
    {
        var selectedWorlds = GetSelectedPreferredWorlds();
        return (LookupScopeKind)cheapestScopeIndex switch
        {
            LookupScopeKind.CurrentDataCenter => LookupScope.CurrentDataCenter,
            LookupScopeKind.CurrentRegion => LookupScope.CurrentRegion,
            LookupScopeKind.CustomRegions => LookupScope.FromCustomRegions(selectedRegions),
            LookupScopeKind.SpecificWorld when selectedWorlds.Count > 0 => LookupScope.FromCustomWorlds(selectedWorlds.ToArray()),
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

    private IReadOnlyList<string> GetSelectedPreferredWorlds()
    {
        var result = new List<string>();
        var normalizedDefault = preferredWorld.Trim();
        if (!string.IsNullOrEmpty(normalizedDefault))
            result.Add(normalizedDefault);

        foreach (var world in preferredWorldSelections)
        {
            if (!result.Any(w => string.Equals(w, world, StringComparison.OrdinalIgnoreCase)))
                result.Add(world);
        }

        return result;
    }

    private void NormalizePreferredWorldSelections()
    {
        for (int i = preferredWorldSelections.Count - 1; i >= 0; i--)
        {
            var normalized = preferredWorldSelections[i]?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                preferredWorldSelections.RemoveAt(i);
                continue;
            }

            preferredWorldSelections[i] = normalized;
        }

        var deduped = preferredWorldSelections
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        preferredWorldSelections.Clear();
        preferredWorldSelections.AddRange(deduped);
    }

    private void PersistPreferredWorldSelections()
    {
        plugin.Configuration.PreferredWorldList = preferredWorldSelections.ToArray();
        plugin.Configuration.Save();
    }

    private void SetPreferredWorldSelections(IEnumerable<string> worlds)
    {
        preferredWorldSelections.Clear();
        foreach (var world in worlds)
        {
            var normalized = world?.Trim();
            if (string.IsNullOrEmpty(normalized))
                continue;

            if (!string.IsNullOrEmpty(preferredWorld) &&
                string.Equals(normalized, preferredWorld, StringComparison.OrdinalIgnoreCase))
                continue;

            if (preferredWorldSelections.Any(w => string.Equals(w, normalized, StringComparison.OrdinalIgnoreCase)))
                continue;

            preferredWorldSelections.Add(normalized);
        }

        PersistPreferredWorldSelections();
    }

    private void RemovePreferredWorldSelectionAt(int index)
    {
        if (index < 0 || index >= preferredWorldSelections.Count)
            return;

        preferredWorldSelections.RemoveAt(index);
        PersistPreferredWorldSelections();
    }

    private void ApplyPreferredWorldSelection(IReadOnlyList<string> worlds)
    {
        SetPreferredWorldSelections(worlds);
    }

    private string DescribeScope(LookupScope scope)
    {
        return scope.Kind switch
        {
            LookupScopeKind.CurrentDataCenter => "your current data center",
            LookupScopeKind.CurrentRegion => "your current region",
            LookupScopeKind.CustomRegions when scope.CustomRegions is { Count: > 0 }
                => $"regions: {string.Join(", ", scope.CustomRegions)}",
            LookupScopeKind.CustomWorlds when scope.CustomWorlds is { Count: > 0 }
                => $"worlds: {string.Join(", ", scope.CustomWorlds)}",
            _ => "your preferred world"
        };
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

        ImGui.TextWrapped("Use the search box to look up an item. The /mqp command still works if you need it.");
        ImGui.Spacing();

        var triggered = false;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var inputWidth = Math.Max(0f, availableWidth - FindCheapestButtonWidth - spacing);
        ImGui.PushItemWidth(inputWidth);
        var submitted = ImGui.InputText("##mqp_item_search", ref itemSearch, SearchInputMaxLength, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.PopItemWidth();
        triggered |= submitted;

        var selectedRegions = GetSelectedCustomRegions();
        var selectedWorlds = GetSelectedPreferredWorlds();
        var scopeKind = (LookupScopeKind)cheapestScopeIndex;
        var hasName = !string.IsNullOrWhiteSpace(itemSearch);
        var selectionValid = scopeKind switch
        {
            LookupScopeKind.CustomRegions => selectedRegions.Count > 0,
            LookupScopeKind.SpecificWorld => selectedWorlds.Count > 0,
            _ => true
        };
        var canRunCheapest = hasName && selectionValid;

        ImGui.SameLine();
        if (!canRunCheapest) ImGui.BeginDisabled();
        var findCheapestPressed = ImGui.Button("Find cheapest", new Vector2(FindCheapestButtonWidth, 0));
        if (!canRunCheapest) ImGui.EndDisabled();
        if (findCheapestPressed)
            RunFindCheapest(selectedRegions);

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

        DrawFindCheapestSection(selectedRegions, selectedWorlds);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawWorldPickers();

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
                    {
                        plugin.LastResult = h;
                        if (!string.IsNullOrWhiteSpace(h.ItemName))
                            ImGui.SetClipboardText(h.ItemName);
                    }

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
        DrawCurrentResultDetails(r);
    }

    private void DrawCurrentResultDetails(Plugin.MarketResult result)
    {
        if (!ImGui.BeginTable("mqp_current_result", 2, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("info", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("icon", ImGuiTableColumnFlags.WidthFixed, ResultIconSize + 12f);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        DrawResultInfoColumn(result);

        ImGui.TableNextColumn();
        DrawResultIconColumn(result);

        ImGui.EndTable();
    }

    private void DrawResultInfoColumn(Plugin.MarketResult result)
    {
        ImGui.TextUnformatted($"{result.ItemName} @ {result.World}");
        ImGui.TextUnformatted($"Lowest: {result.Lowest:n0} gil");
        var updated = DateTimeOffset.FromUnixTimeMilliseconds(result.LastUploadMs).LocalDateTime;
        ImGui.TextUnformatted($"Last updated: {updated:g}");
        ImGui.TextUnformatted($"Listings: {result.TotalListings:n0} ({result.TotalListedQuantity:n0} items)");

        if (result.LatestSale is { } sale)
        {
            var saleTime = DateTimeOffset.FromUnixTimeSeconds(sale.Timestamp).LocalDateTime;
            ImGui.TextUnformatted($"Latest sale: {sale.Quantity:n0} @ {sale.PricePerUnit:n0} gil ({saleTime:g})");
        }
        else
        {
            ImGui.TextUnformatted("Latest sale: No data");
        }

        if (result.DaySalesQuantity > 0)
        {
            var saleWord = result.DaySalesCount == 1 ? "sale" : "sales";
            ImGui.TextUnformatted($"Sold (24h): {result.DaySalesQuantity:n0} items across {result.DaySalesCount:n0} {saleWord}");
        }
        else
        {
            ImGui.TextUnformatted("Sold (24h): No recorded sales");
        }

        ImGui.Spacing();
        if (ImGui.Button("Settings")) plugin.ToggleConfigUi();
    }

    private void DrawResultIconColumn(Plugin.MarketResult result)
    {
        var size = new Vector2(ResultIconSize, ResultIconSize);
        if (result.IconId == 0)
        {
            ImGui.Dummy(size);
            return;
        }

        try
        {
            var lookup = new GameIconLookup(result.IconId, itemHq: false, hiRes: true);
            var shared = Plugin.TextureProvider.GetFromGameIcon(lookup);
            var wrap = shared.GetWrapOrEmpty();

            var cursor = ImGui.GetCursorPos();
            var available = ImGui.GetContentRegionAvail();
            var offsetX = Math.Max(0f, (available.X - size.X) * 0.5f);
            ImGui.SetCursorPosX(cursor.X + offsetX);
            ImGui.Image(wrap.Handle, size);
        }
        catch
        {
            ImGui.Dummy(size);
        }
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
            var selections = GetSelectedPreferredWorlds();
            worldPicker.Open(preferredWorld, selections, world => UpdatePreferredWorld(world), ApplyPreferredWorldSelection);
        }

        var storedWorld = plugin.Configuration.DefaultWorld ?? string.Empty;
        var current = string.IsNullOrWhiteSpace(storedWorld)
            ? "Using current character world"
            : $"Using: {storedWorld}";
        ImGui.TextUnformatted(current);
    }

    private void DrawPreferredWorldSelectionPanel()
    {
        DrawWorldPreferenceSection();
        ImGui.Spacing();
        ImGui.TextUnformatted("Additional preferred worlds");

        if (preferredWorldSelections.Count == 0)
        {
            ImGui.TextDisabled("No additional worlds selected.");
        }
        else
        {
            for (int i = 0; i < preferredWorldSelections.Count; i++)
            {
                var world = preferredWorldSelections[i];
                ImGui.PushID($"pref_world_{i}");
                ImGui.TextUnformatted(world);
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    RemovePreferredWorldSelectionAt(i);
                    ImGui.PopID();
                    i--;
                    continue;
                }
                ImGui.PopID();
            }
        }
        ImGui.TextDisabled("Use \"Choose from list\" to update the selections.");
    }

    private void DrawWorldPickers()
        => worldPicker.Draw();

    private void DrawFindCheapestSection(IReadOnlyList<string> selectedRegions, IReadOnlyList<string> selectedWorlds)
    {
        ImGui.TextUnformatted("Find cheapest in:");
        var currentScope = cheapestScopeIndex;
        if (ImGui.RadioButton("Current data center", ref currentScope, (int)LookupScopeKind.CurrentDataCenter))
            cheapestScopeIndex = currentScope;

        if (ImGui.RadioButton("Current region", ref currentScope, (int)LookupScopeKind.CurrentRegion))
            cheapestScopeIndex = currentScope;

        if (ImGui.RadioButton("Preferred world", ref currentScope, (int)LookupScopeKind.SpecificWorld))
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
        else if ((LookupScopeKind)cheapestScopeIndex == LookupScopeKind.SpecificWorld)
        {
            ImGui.Indent();
            DrawPreferredWorldSelectionPanel();
            ImGui.Unindent();
        }

        var hasName = !string.IsNullOrWhiteSpace(itemSearch);
        var scopeKind = (LookupScopeKind)cheapestScopeIndex;
        var selectionValid = scopeKind switch
        {
            LookupScopeKind.CustomRegions => selectedRegions.Count > 0,
            LookupScopeKind.SpecificWorld => selectedWorlds.Count > 0,
            _ => true
        };

        if (!hasName)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.35f, 0.35f, 1f), "Enter an item name above.");
        }
        else if (!selectionValid)
        {
            var warning = scopeKind == LookupScopeKind.CustomRegions
                ? "Select at least one region."
                : "Select at least one world.";
            ImGui.TextColored(new Vector4(0.9f, 0.35f, 0.35f, 1f), warning);
        }
    }

    private void RunFindCheapest(IReadOnlyList<string> selectedRegions)
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

    private void UpdatePreferredWorld(string rawValue, string? displayOverride = null)
    {
        var normalized = rawValue.Trim();
        var displayValue = displayOverride ?? normalized;
        if (!string.Equals(preferredWorld, displayValue, StringComparison.Ordinal))
            preferredWorld = displayValue;

        var removed = preferredWorldSelections.RemoveAll(w => string.Equals(w, preferredWorld, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            PersistPreferredWorldSelections();

        if (string.Equals(plugin.Configuration.DefaultWorld ?? string.Empty, normalized, StringComparison.Ordinal))
            return;

        plugin.Configuration.DefaultWorld = normalized;
        plugin.Configuration.Save();
    }

    private void TrackWindowSizeChange()
    {
        var currentSize = ImGui.GetWindowSize();
        if (Vector2.DistanceSquared(currentSize, trackedWindowSize) <= SizeChangeThreshold * SizeChangeThreshold)
            return;

        trackedWindowSize = currentSize;
        Size = currentSize;
        plugin.Configuration.MainWindowSize = currentSize;
        sizeDirtySince = DateTime.UtcNow;
        sizeDirty = true;
    }

    private void SaveWindowSizeIfNeeded(bool force = false)
    {
        if (!sizeDirty)
            return;

        if (!force && (DateTime.UtcNow - sizeDirtySince) < SizeSaveDelay)
            return;

        plugin.Configuration.Save();
        sizeDirty = false;
    }
}
