using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameLauncher;

public class AppSettings
{
    // Loaded from .env (STEAMGRID_API_KEY), never written to settings.json
    [JsonIgnore]
    public string SteamGridApiKey { get; set; } = LoadEnvValue("STEAMGRID_API_KEY") ?? "";
    public int BannerWidth { get; set; } = 150;
    public int BannerHeight { get; set; } = 225;
    public string BackgroundColor { get; set; } = "#121212";
    public string NavbarColor { get; set; } = "#1e1e1e";
    public string LogoColor { get; set; } = "Cyan";
    public double RecentsBannerWidth { get; set; } = 80; // Default width
    public double RecentsBannerHeight { get; set; } = 120; // Default height
    public string GameLibraryPath { get; set; } = @"";

    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    private static readonly string CategoriesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game_categories.json");
    private static readonly string UserCategoriesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_categories.json");

    private static string? LoadEnvValue(string key)
    {
        // Real environment variables take priority over the .env file
        string? value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value)) return value;

        string envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        if (!File.Exists(envPath)) return null;

        foreach (string rawLine in File.ReadAllLines(envPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            if (line[..eq].Trim() == key)
                return line[(eq + 1)..].Trim().Trim('"');
        }
        return null;
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { /* Fallback to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public static Dictionary<string, string> LoadGameCategories()
    {
        try
        {
            if (File.Exists(CategoriesPath))
            {
                string json = File.ReadAllText(CategoriesPath);
                var categories = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return categories ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { /* Fallback to defaults */ }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static void SaveGameCategories(Dictionary<string, string> categories)
    {
        string json = JsonSerializer.Serialize(categories, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(CategoriesPath, json);
    }

    public static Dictionary<string, string> LoadUserCategories()
    {
        try
        {
            if (File.Exists(UserCategoriesPath))
            {
                string json = File.ReadAllText(UserCategoriesPath);
                var categories = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return categories ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { /* Fallback to defaults */ }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static void SaveUserCategories(Dictionary<string, string> categories)
    {
        string json = JsonSerializer.Serialize(categories, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(UserCategoriesPath, json);
    }
}