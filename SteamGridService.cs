using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameLauncher;

public class SteamGridService
{
    private static readonly HttpClient _client = new HttpClient();

    public static async Task<string?> GetGridUrlAsync(string gameName, string apiKey)
    {
        // 1. Check local Assets folder first
        string assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
        if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);
        
        string localFilePath = Path.Combine(assetsDir, $"{gameName}.jpg");
        if (File.Exists(localFilePath)) return localFilePath;

        // 2. If not local, call API
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var searchResponse = await _client.GetAsync($"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(gameName)}");
            var searchJson = await JsonDocument.ParseAsync(await searchResponse.Content.ReadAsStreamAsync());
            
            if (searchJson.RootElement.GetProperty("success").GetBoolean() && searchJson.RootElement.GetProperty("data").GetArrayLength() > 0)
            {
                var gameId = searchJson.RootElement.GetProperty("data")[0].GetProperty("id").GetInt32();
                var gridResponse = await _client.GetAsync($"https://www.steamgriddb.com/api/v2/grids/game/{gameId}?dimensions=600x900");
                var gridJson = await JsonDocument.ParseAsync(await gridResponse.Content.ReadAsStreamAsync());

                if (gridJson.RootElement.GetProperty("success").GetBoolean() && gridJson.RootElement.GetProperty("data").GetArrayLength() > 0)
                {
                    string remoteUrl = gridJson.RootElement.GetProperty("data")[0].GetProperty("url").GetString()!;
                    
                    // 3. Download and Save locally
                    var imageBytes = await _client.GetByteArrayAsync(remoteUrl);
                    await File.WriteAllBytesAsync(localFilePath, imageBytes);
                    
                    return localFilePath;
                }
            }
        }
        catch { return null; }
        return null;
    }
}