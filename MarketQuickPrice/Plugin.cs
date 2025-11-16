using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Inventory;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MarketQuickPrice.Windows;
using MarketQuickPrice.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Lumina.Excel.Sheets;

namespace MarketQuickPrice;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;

    private const string CommandName = "/mqp";
    private const int LookupCooldownSeconds = 2;

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("MarketQuickPrice");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly HttpClient http = new();
    private DateTime lastCall = DateTime.MinValue;

    internal record MarketResult(
        uint ItemId,
        uint IconId,
        string ItemName,
        string World,
        long Lowest,
        long LastUploadMs,
        int TotalListings,
        int TotalListedQuantity,
        MarketSale? LatestSale,
        int DaySalesQuantity,
        int DaySalesCount);

    internal readonly record struct MarketSale(long PricePerUnit, int Quantity, long Timestamp);

    internal readonly List<MarketResult> History = new();
    internal MarketResult? LastResult { get; set; }

    private enum LookupTargetType
    {
        World,
        DataCenter,
        Region
    }

    private readonly record struct LookupTarget(LookupTargetType Type, string Identifier, string Label);

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Quick market price lookup. Usage: /mqp <item name>"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        ContextMenu.OnMenuOpened += OnMenuOpened;

        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        ContextMenu.OnMenuOpened -= OnMenuOpened;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        Configuration.Save();
    }

    private void OnCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Chat.Print("[MQP] Opening the main window. Use the search box there to look up an item.");
            MainWindow.IsOpen = true;
            return;
        }

        if (!TryBeginLookup(args, out var error, LookupScope.SpecificWorld))
        {
            Chat.Print($"[MQP] {error}");
        }
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.MenuType != ContextMenuType.Inventory)
            return;

        if (args.Target is not MenuTargetInventory target)
            return;

        if (target.TargetItem is not GameInventoryItem item || item.BaseItemId == 0)
            return;

        var row = FindItem(item.BaseItemId);
        if (!row.HasValue)
            return;

        var itemName = row.Value.Name.ToString();

        if (string.IsNullOrWhiteSpace(itemName))
            return;

        args.AddMenuItem(new MenuItem
        {
            Name = "Check Price with MQP",
            OnClicked = _ => BeginContextLookup(itemName)
        });
    }

    private void BeginContextLookup(string itemName)
    {
        if (TryBeginLookup(itemName, out var error, LookupScope.SpecificWorld))
            return;

        if (!string.IsNullOrEmpty(error))
            Chat.Print($"[MQP] {error}");
    }

    internal bool TryBeginLookup(string? rawQuery, out string errorMessage, LookupScope? scopeOverride = null)
    {
        errorMessage = string.Empty;
        var query = rawQuery?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            errorMessage = "Please enter an item name.";
            return false;
        }

        var minSeconds = Math.Max(1, LookupCooldownSeconds);
        var sinceLast = (DateTime.UtcNow - lastCall).TotalSeconds;
        if (sinceLast < minSeconds)
        {
            var remaining = Math.Max(1, (int)Math.Ceiling(minSeconds - sinceLast));
            errorMessage = $"Please wait {remaining} more second(s) before querying again.";
            return false;
        }

        var item = FindItem(query);

        if (!item.HasValue)
        {
            errorMessage = $"Couldn't find an item named '{query}'.";
            return false;
        }

        lastCall = DateTime.UtcNow;
        var scope = scopeOverride ?? LookupScope.SpecificWorld;

        var iconId = (uint)item.Value.Icon;
        _ = QueryMarketAsync(item.Value.RowId, item.Value.Name.ToString(), scope, iconId);
        MainWindow.IsOpen = true;
        return true;
    }

    private Item? FindItem(string query)
    {
        var sheet = DataManager.GetExcelSheet<Item>();
        if (sheet == null) return null;
        query = query.Trim();

        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (!string.IsNullOrEmpty(name) &&
                string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
                return row;
        }

        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (!string.IsNullOrEmpty(name) &&
                name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return row;
        }

        foreach (var row in sheet)
        {
            var name = row.Name.ToString();
            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return row;
        }

        return null;
    }

    private Item? FindItem(uint rowId)
    {
        var sheet = DataManager.GetExcelSheet<Item>();
        if (sheet == null) return null;

        foreach (var row in sheet)
        {
            if (row.RowId == rowId)
                return row;
        }

        return null;
    }

    private record UniversalisListing(long pricePerUnit, int quantity, string worldName);
    private record UniversalisHistory(long pricePerUnit, int quantity, long timestamp);
    private record UniversalisResp(long lastUploadTime, List<UniversalisListing> listings, List<UniversalisHistory>? recentHistory);

    private void AddToHistory(MarketResult r) {
        History.RemoveAll(x =>
            string.Equals(x.ItemName, r.ItemName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.World, r.World, StringComparison.Ordinal));

        History.Insert(0, r);

        var max = Math.Clamp(Configuration.HistoryCapacity, 5, 30);
        if (History.Count > max) {
            History.RemoveRange(max, History.Count - max);
        }
    }

    private async Task QueryMarketAsync(uint itemId, string itemName, LookupScope scope, uint iconId)
    {
        try
        {
            var targets = BuildLookupTargets(scope);
            var targetSummary = string.Join(", ", targets.Select(t => t.Label));

            MarketResult? bestResult = null;

            foreach (var target in targets)
            {
                var result = await QueryMarketForTargetAsync(itemId, itemName, iconId, target);
                if (result is null)
                    continue;

                if (bestResult is null || result.Lowest < bestResult.Lowest)
                    bestResult = result;
            }

            if (bestResult is null)
            {
                Chat.Print($"[MQP] No market listings for {itemName} in {targetSummary}.");
                return;
            }

            LastResult = bestResult;
            AddToHistory(bestResult);

            Chat.Print($"[MQP] {itemName} @ {bestResult.World}: {bestResult.Lowest:n0} gil (latest)");
            MainWindow.IsOpen = true;
        }
        catch (Exception ex)
        {
            Chat.PrintError($"[MQP] Error: {ex.Message}");
            Log.Error(ex, "QueryMarketAsync failed");
        }
    }

    private async Task<MarketResult?> QueryMarketForTargetAsync(uint itemId, string itemName, uint iconId, LookupTarget target)
    {
        var url = $"https://universalis.app/api/v2/{Uri.EscapeDataString(target.Identifier)}/{itemId}?listings=100&entries=100";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("Dalamud-MarketQuickPrice/1.0");

        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<UniversalisResp>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data?.listings is null || data.listings.Count == 0)
            return null;

        var lowest = data.listings.MinBy(l => l.pricePerUnit)!;
        var worldLabel = target.Type == LookupTargetType.World
            ? target.Label
            : $"{lowest.worldName} [{target.Label}]";

        var totalListings = data.listings.Count;
        var totalQuantity = data.listings.Sum(l => l.quantity);

        MarketSale? latestSale = null;
        var daySalesQuantity = 0;
        var daySalesCount = 0;
        var cutoffSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86_400;

        if (data.recentHistory is { Count: > 0 })
        {
            long latestTimestamp = long.MinValue;
            UniversalisHistory? mostRecent = null;

            foreach (var entry in data.recentHistory)
            {
                if (entry.timestamp > latestTimestamp)
                {
                    latestTimestamp = entry.timestamp;
                    mostRecent = entry;
                }

                if (entry.timestamp >= cutoffSeconds)
                {
                    daySalesQuantity += entry.quantity;
                    daySalesCount++;
                }
            }

            if (mostRecent is not null)
                latestSale = new MarketSale(mostRecent.pricePerUnit, mostRecent.quantity, mostRecent.timestamp);
        }

        return new MarketResult(    
            itemId,
            iconId,
            itemName,
            worldLabel,
            lowest.pricePerUnit,
            data.lastUploadTime,
            totalListings,
            totalQuantity,
            latestSale,
            daySalesQuantity,
            daySalesCount);
    }

    private IReadOnlyList<LookupTarget> BuildLookupTargets(LookupScope scope)
    {
        return scope.Kind switch
        {
            LookupScopeKind.CurrentDataCenter => TryGetCurrentDataCenterTarget(out var dcTarget)
                ? new[] { dcTarget }
                : new[] { CreateWorldTarget(GetPreferredWorldName()) },
            LookupScopeKind.CurrentRegion => TryGetCurrentRegionTarget(out var regionTarget)
                ? new[] { regionTarget }
                : new[] { CreateWorldTarget(GetPreferredWorldName()) },
            LookupScopeKind.CustomRegions => BuildCustomRegionTargets(scope.CustomRegions),
            LookupScopeKind.CustomWorlds => BuildCustomWorldTargets(scope.CustomWorlds),
            _ => new[] { CreateWorldTarget(GetPreferredWorldName()) }
        };
    }

    private IReadOnlyList<LookupTarget> BuildCustomRegionTargets(IReadOnlyList<string>? regions)
    {
        if (regions is null || regions.Count == 0)
            return new[] { CreateWorldTarget(GetPreferredWorldName()) };

        var list = new List<LookupTarget>();
        foreach (var name in regions)
        {
            if (!WorldRegions.TryGetRegionByName(name, out var region))
                continue;

            if (list.Any(t => string.Equals(t.Identifier, region.ApiIdentifier, StringComparison.OrdinalIgnoreCase)))
                continue;

            list.Add(CreateRegionTarget(region));
        }

        return list.Count > 0 ? list : new[] { CreateWorldTarget(GetPreferredWorldName()) };
    }

    private IReadOnlyList<LookupTarget> BuildCustomWorldTargets(IReadOnlyList<string>? worlds)
    {
        if (worlds is null || worlds.Count == 0)
            return new[] { CreateWorldTarget(GetPreferredWorldName()) };

        var list = new List<LookupTarget>();
        foreach (var world in worlds)
        {
            var normalized = world?.Trim();
            if (string.IsNullOrEmpty(normalized))
                continue;

            if (list.Any(t => string.Equals(t.Identifier, normalized, StringComparison.OrdinalIgnoreCase)))
                continue;

            list.Add(CreateWorldTarget(normalized));
        }

        return list.Count > 0 ? list : new[] { CreateWorldTarget(GetPreferredWorldName()) };
    }

    private bool TryGetCurrentDataCenterTarget(out LookupTarget target)
    {
        target = default;
        var currentWorld = GetCurrentWorldName();
        if (currentWorld is null)
            return false;

        if (!WorldRegions.TryGetWorldInfo(currentWorld, out var region, out var dataCenter))
            return false;

        target = CreateDataCenterTarget(dataCenter, region);
        return true;
    }

    private bool TryGetCurrentRegionTarget(out LookupTarget target)
    {
        target = default;
        var currentWorld = GetCurrentWorldName();
        if (currentWorld is null)
            return false;

        if (!WorldRegions.TryGetWorldInfo(currentWorld, out var region, out _))
            return false;

        target = CreateRegionTarget(region);
        return true;
    }

    private LookupTarget CreateWorldTarget(string world)
        => new(LookupTargetType.World, world, world);

    private LookupTarget CreateDataCenterTarget(DataCenterInfo dataCenter, RegionInfo region)
        => new(LookupTargetType.DataCenter, dataCenter.ApiIdentifier, $"{dataCenter.Name} ({region.Name})");

    private LookupTarget CreateRegionTarget(RegionInfo region)
        => new(LookupTargetType.Region, region.ApiIdentifier, region.Name);

    private string GetPreferredWorldName()
    {
        if (!string.IsNullOrWhiteSpace(Configuration.DefaultWorld))
            return Configuration.DefaultWorld;

        return GetCurrentWorldName() ?? "Unknown";
    }

    private string? GetCurrentWorldName()
        => ClientState.LocalPlayer is { } lp && lp.CurrentWorld.IsValid
            ? lp.CurrentWorld.Value.Name.ToString()
            : null;

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
