using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameLauncher;

public class SteamGridMetadata
{
    public string? GridUrl { get; set; }
    public string Category { get; set; } = "Uncategorized";
}

public class SteamGridService
{
    private static readonly HttpClient _client = new HttpClient();

    public static async Task<SteamGridMetadata?> GetGridUrlAsync(string gameName, string apiKey)
    {
        string assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
        if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);

        string localFilePath = Path.Combine(assetsDir, $"{gameName}.jpg");
        var categories = AppSettings.LoadGameCategories();
        if (categories.TryGetValue(gameName, out var cachedCategoryFromFile) && !string.IsNullOrWhiteSpace(cachedCategoryFromFile))
        {
            return new SteamGridMetadata
            {
                GridUrl = File.Exists(localFilePath) ? localFilePath : null,
                Category = cachedCategoryFromFile
            };
        }

        var fallbackCategory = InferCategoryFromGameName(gameName);
        categories[gameName] = fallbackCategory;
        AppSettings.SaveGameCategories(categories);

        if (File.Exists(localFilePath))
        {
            return new SteamGridMetadata
            {
                GridUrl = localFilePath,
                Category = fallbackCategory
            };
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var searchResponse = await _client.GetAsync($"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(gameName)}");
            var searchJson = await JsonDocument.ParseAsync(await searchResponse.Content.ReadAsStreamAsync());

            if (searchJson.RootElement.GetProperty("success").GetBoolean() && searchJson.RootElement.GetProperty("data").GetArrayLength() > 0)
            {
                var firstMatch = searchJson.RootElement.GetProperty("data")[0];
                var gameId = firstMatch.GetProperty("id").GetInt32();
                var gridResponse = await _client.GetAsync($"https://www.steamgriddb.com/api/v2/grids/game/{gameId}?dimensions=600x900");
                var gridJson = await JsonDocument.ParseAsync(await gridResponse.Content.ReadAsStreamAsync());

                if (gridJson.RootElement.GetProperty("success").GetBoolean() && gridJson.RootElement.GetProperty("data").GetArrayLength() > 0)
                {
                    string remoteUrl = gridJson.RootElement.GetProperty("data")[0].GetProperty("url").GetString()!;
                    var imageBytes = await _client.GetByteArrayAsync(remoteUrl);
                    await File.WriteAllBytesAsync(localFilePath, imageBytes);

                    return new SteamGridMetadata
                    {
                        GridUrl = localFilePath,
                        Category = fallbackCategory
                    };
                }
            }
        }
        catch { }

        return new SteamGridMetadata
        {
            GridUrl = null,
            Category = categories.TryGetValue(gameName, out var cachedCategoryFromMap) && !string.IsNullOrWhiteSpace(cachedCategoryFromMap)
                ? cachedCategoryFromMap
                : "Uncategorized"
        };
    }

    private static string InferCategoryFromGameName(string gameName)
    {
        var normalized = gameName.ToLowerInvariant();

        if (normalized.Contains("minecraft") || normalized.Contains("terraria") || normalized.Contains("valheim") || normalized.Contains("roblox") || normalized.Contains("stardew") || normalized.Contains("sim") || normalized.Contains("craft"))
            return "Sandbox / Survival";

        if (normalized.Contains("counter") || normalized.Contains("battlefield") || normalized.Contains("call of duty") || normalized.Contains("warzone") || normalized.Contains("valorant") || normalized.Contains("fortnite") || normalized.Contains("pubg") || normalized.Contains("apex"))
            return "Shooter";

        if (normalized.Contains("racing") || normalized.Contains("forza") || normalized.Contains("need for speed") || normalized.Contains("gran turismo") || normalized.Contains("nfs") || normalized.Contains("drift") || normalized.Contains("motor") || normalized.Contains("track"))
            return "Racing";

        if (normalized.Contains("football") || normalized.Contains("soccer") || normalized.Contains("nba") || normalized.Contains("fifa") || normalized.Contains("madden") || normalized.Contains("nhl") || normalized.Contains("sports"))
            return "Sports";

        if (normalized.Contains("race") || normalized.Contains("puzzle") || normalized.Contains("platform") || normalized.Contains("arcade") || normalized.Contains("adventure") || normalized.Contains("strategy") || normalized.Contains("rpg") || normalized.Contains("roguelike") || normalized.Contains("rogue"))
            return "Adventure / Arcade";

        return "Uncategorized";
    }
}