using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace GameLauncher;

/// <summary>
/// Finds games installed via third-party launchers (Epic, Ubisoft Connect, GOG Galaxy,
/// Microsoft Store / Xbox) by reading each launcher's own install records, instead of
/// guessing from folder names. This is far more reliable than fuzzy-matching folders,
/// and it finds games no matter which drive they're installed on.
/// </summary>
public static class LauncherDetectionService
{
    private static List<(string Name, string Path)>? _cache;

    /// <summary>Name/install-path pairs for every game this service could find. Cached after first call.</summary>
    public static List<(string Name, string Path)> GetAllInstalledGames()
    {
        if (_cache != null) return _cache;

        var results = new List<(string, string)>();
        try { results.AddRange(GetEpicGames()); } catch { }
        try { results.AddRange(GetUbisoftGames()); } catch { }
        try { results.AddRange(GetGogGames()); } catch { }
        try { results.AddRange(GetMsStoreGames()); } catch { }

        _cache = results;
        return results;
    }

    /// <summary>Call this if you want a fresh scan (e.g. user just installed something and clicked "Refresh").</summary>
    public static void InvalidateCache() => _cache = null;

    // ---- Epic Games Launcher -------------------------------------------------
    // Every installed game gets a .item manifest (JSON) in this folder, containing
    // a human-readable DisplayName and the real InstallLocation on disk.
    private static List<(string, string)> GetEpicGames()
    {
        var list = new List<(string, string)>();
        string manifestDir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
        if (!Directory.Exists(manifestDir)) return list;

        foreach (var file in Directory.EnumerateFiles(manifestDir, "*.item"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;

                string? displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
                string? installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;

                if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(installLocation))
                {
                    list.Add((displayName!, installLocation!));
                }
            }
            catch { /* skip a corrupt/partial manifest */ }
        }
        return list;
    }

    // ---- Ubisoft Connect -------------------------------------------------
    // Each installed game gets a numeric subkey under Installs, with an InstallDir value.
    // There's no friendly name stored here, so we use the install folder's name.
    private static List<(string, string)> GetUbisoftGames()
    {
        var list = new List<(string, string)>();
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs")
                      ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs");
        if (key == null) return list;

        foreach (var gameId in key.GetSubKeyNames())
        {
            try
            {
                using var gameKey = key.OpenSubKey(gameId);
                var installDir = gameKey?.GetValue("InstallDir") as string;
                if (!string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir))
                {
                    var name = Path.GetFileName(installDir.TrimEnd('\\', '/'));
                    list.Add((string.IsNullOrWhiteSpace(name) ? installDir : name, installDir));
                }
            }
            catch { }
        }
        return list;
    }

    // ---- GOG Galaxy -------------------------------------------------
    // Each installed game gets a numeric subkey under Games, with "path" and "gameName" values.
    private static List<(string, string)> GetGogGames()
    {
        var list = new List<(string, string)>();
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games")
                      ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games");
        if (key == null) return list;

        foreach (var gameId in key.GetSubKeyNames())
        {
            try
            {
                using var gameKey = key.OpenSubKey(gameId);
                var path = gameKey?.GetValue("path") as string;
                var name = gameKey?.GetValue("gameName") as string;
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    list.Add((string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path.TrimEnd('\\', '/')) : name!, path));
                }
            }
            catch { }
        }
        return list;
    }

    // ---- Microsoft Store / Xbox / Game Pass (packaged apps) -------------------------------------------------
    // Installed appx/msix packages are listed here per-user, each with its real install folder.
    // Package full names look like "Microsoft.SeaOfThieves_2.0.0.0_x64__8wekyb3d8bbwe" — we strip
    // the version/architecture/publisher-hash suffix and the publisher prefix to get something
    // that can be fuzzy-matched against a shortcut's display name.
    private static List<(string, string)> GetMsStoreGames()
    {
        var list = new List<(string, string)>();

        using (var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
        {
            if (key != null)
            {
                foreach (var packageFullName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var pkgKey = key.OpenSubKey(packageFullName);
                        var rootFolder = pkgKey?.GetValue("PackageRootFolder") as string;
                        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder)) continue;

                        var friendly = packageFullName.Split('_')[0];       // drop _version_arch__hash
                        var lastDot = friendly.LastIndexOf('.');
                        if (lastDot >= 0 && lastDot < friendly.Length - 1)
                            friendly = friendly.Substring(lastDot + 1);      // drop "Microsoft." / publisher prefix

                        list.Add((friendly, rootFolder!));
                    }
                    catch { }
                }
            }
        }

        // Default Xbox/Game Pass PC install location (per drive, since users can pick a drive during setup).
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                var xboxGames = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
                if (Directory.Exists(xboxGames))
                {
                    foreach (var dir in Directory.EnumerateDirectories(xboxGames))
                    {
                        list.Add((Path.GetFileName(dir), dir));
                    }
                }
            }
        }
        catch { }

        return list;
    }
}
