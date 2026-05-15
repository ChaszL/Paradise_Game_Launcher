using System;
using System.IO;
using System.Text.Json;

namespace GameLauncher;

public class AppSettings
{
    public string SteamGridApiKey { get; set; } = "5f7b7a352ae31a15d4f5adde3bad2227";
    public int BannerWidth { get; set; } = 150;
    public int BannerHeight { get; set; } = 225;
    public string BackgroundColor { get; set; } = "#121212";
    public string NavbarColor { get; set; } = "#1e1e1e";
    public string LogoColor { get; set; } = "Cyan";
    public double RecentsBannerWidth { get; set; } = 80; // Default width
    public double RecentsBannerHeight { get; set; } = 120; // Default height
    public string GameLibraryPath { get; set; } = @"C:\Users\chasz\Downloads\Programs\PYTHON\Game_launcher\games";

    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

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
}