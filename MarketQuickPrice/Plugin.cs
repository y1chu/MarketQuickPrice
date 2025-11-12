using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MarketQuickPrice.Windows;
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

    private const string CommandName = "/mqp";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("MarketQuickPrice");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly HttpClient http = new();
    private DateTime lastCall = DateTime.MinValue;

    internal record MarketResult(string ItemName, string World, long Lowest, long LastUploadMs);
    internal readonly List<MarketResult> History = new();
    internal MarketResult? LastResult { get; set; }

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

        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Chat.Print("Usage: /mqp <item name>");
            return;
        }

        if ((DateTime.UtcNow - lastCall).TotalSeconds < Math.Max(1, Configuration.MinSecondsBetweenCalls))
        {
            Chat.Print("[MQP] Please wait a moment before querying again.");
            return;
        }

        var item = FindItem(args);

        if (!item.HasValue)
        {
            Chat.Print($"[MQP] Couldn't find an item named '{args}'.");
            return;
        }

        lastCall = DateTime.UtcNow;

        _ = QueryMarketAsync(item.Value.RowId, item.Value.Name.ToString());
        MainWindow.IsOpen = true;
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

    private record UniversalisListing(long pricePerUnit, int quantity, string worldName);
    private record UniversalisResp(long lastUploadTime, List<UniversalisListing> listings);

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

    private async Task QueryMarketAsync(uint itemId, string itemName)
    {
        try
        {
            var world = !string.IsNullOrWhiteSpace(Configuration.DefaultWorld)
                ? Configuration.DefaultWorld
                : (ClientState.LocalPlayer is { } lp && lp.CurrentWorld.IsValid
                    ? lp.CurrentWorld.Value.Name.ToString()
                    : "Unknown");

            var url = $"https://universalis.app/api/v2/{Uri.EscapeDataString(world)}/{itemId}?listings=10";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("Dalamud-MarketQuickPrice/1.0");

            using var res = await http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<UniversalisResp>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data?.listings is null || data.listings.Count == 0)
            {
                Chat.Print($"[MQP] No market listings for {itemName} on {world}.");
                return;
            }

            var lowest = data.listings.MinBy(l => l.pricePerUnit)!;

            LastResult = new MarketResult(itemName, world, lowest.pricePerUnit, data.lastUploadTime);
            AddToHistory(LastResult);

            Chat.Print($"[MQP] {itemName} @ {world}: {lowest.pricePerUnit:n0} gil (latest)");
            MainWindow.IsOpen = true;
        }
        catch (Exception ex)
        {
            Chat.PrintError($"[MQP] Error: {ex.Message}");
            Log.Error(ex, "QueryMarketAsync failed");
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
