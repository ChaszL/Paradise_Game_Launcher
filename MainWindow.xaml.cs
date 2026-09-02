using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GameLauncher;

public class ProgressToDashArrayConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        double percentage = 0;
        if (value is double d) percentage = d;
        else if (value is float f) percentage = f;
        else if (value is int i) percentage = i;

        // Keep bounds between 0% and 100%
        percentage = Math.Max(0, Math.Min(100, percentage));

        // The parameter represents the relative circumference of the circle
        double circumference = double.Parse((string)parameter, System.Globalization.CultureInfo.InvariantCulture);
        double dashLength = (percentage / 100.0) * circumference;
        
        // Return the filled amount, followed by a massive empty gap (1000) so it doesn't repeat
        return new System.Windows.Media.DoubleCollection { dashLength, 1000 }; 
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) 
        => throw new NotImplementedException();
}

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public AppSettings CurrentSettings { get; set; }
    public ObservableCollection<GameData> Games { get; set; } = new();
    public ObservableCollection<GameData> RecentGames { get; set; } = new();
    public ObservableCollection<GameData> FilteredGames { get; set; } = new();
    public ObservableCollection<string> Categories { get; set; } = new() { "All" };
    public ObservableCollection<string> FilterCategories { get; set; } = new() { "All" };
    public ObservableCollection<string> AssignableCategories { get; set; } = new();
    private readonly Dictionary<string, string> _userCategories = AppSettings.LoadUserCategories();
    private readonly HashSet<string> _customCategoryOptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _defaultCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "All", "Uncategorized", "Action", "RPG", "Shooter", "Racing", "Sports", "Strategy", "Sandbox",
        "Arcade", "Simulation", "Platformer", "Horror", "Puzzle", "Indie", "Co-op", "Adventure",
        "Casual", "Multiplayer", "Singleplayer", "Story", "Competitive", "Crafting", "Management",
        "Survival", "VR", "Retro", "Open World", "Exploration", "Party", "Anime", "Fantasy", "Sci-Fi"
    };

    private string _selectedCategory = "All";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory != value)
            {
                _selectedCategory = value;
                NotifyPropertyChanged();
                RefreshFilteredGames();
            }
        }
    }

    private HardwareMonitorService _monitor = new();
    private HardwareInfo _stats = new();
    private bool _isUpdatingColorControls;
    private bool _isDraggingSv;
    private string _activeColorTarget = "Background";
    private double _pickerHue;
    private double _pickerSaturation;
    private double _pickerValue;

    public HardwareInfo Stats
    {
        get => _stats;
        set
        {
            _stats = value;
            NotifyPropertyChanged();
        }
    }

    private static readonly string RecentsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recents.json");

    public MainWindow()
    {
        CurrentSettings = AppSettings.Load();
        this.DataContext = this; 
        InitializeComponent();

        GameItemsControl.ItemsSource = FilteredGames;
        SelectedCategory = "All";
        foreach (var category in _defaultCategories)
        {
            if (!Categories.Contains(category))
            {
                Categories.Add(category);
            }
        }

        foreach (var category in _userCategories.Values)
        {
            AddCustomCategoryOption(category);
        }

        RefreshCategoryCollections();
        _ = LoadGamesAsync();
        StartHardwarePolling();
    }

    protected override void OnClosed(EventArgs e)
    {
        _monitor.Dispose();
        base.OnClosed(e);
    }

    private void StartHardwarePolling()
    {
        var timer = new System.Windows.Threading.DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(2); 
        timer.Tick += (s, e) => 
        {
            var metrics = _monitor.GetMetrics();
            
            // UPDATE THE RAW VALUES INSTEAD OF THE USAGE STRINGS
            Stats.CPUValue = metrics.CPUValue;
            Stats.GPUValue = metrics.GPUValue;
            Stats.RAMValue = metrics.RAMValue;
            
            NotifyPropertyChanged(nameof(Stats));
        };
        timer.Start();
    }

    private async Task LoadGamesAsync()
    {
        string targetPath = CurrentSettings.GameLibraryPath;
        if (!Directory.Exists(targetPath)) return;

        var files = Directory.GetFiles(targetPath).Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".url", StringComparison.OrdinalIgnoreCase));
        var filesList = files.ToList();

        if (filesList.Count > 0)
        {
            foreach (var file in filesList)
            {
                var game = GameFactory.CreateFromPath(file);
                Games.Add(game);

                var metadata = await SteamGridService.GetGridUrlAsync(game.Name, CurrentSettings.SteamGridApiKey);
                game.GridUrl = metadata?.GridUrl;

                // Try to resolve shortcut target; if it points to an existing file/folder, treat as installed
                try
                {
                    var target = ResolveShortcutTarget(file);
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        if (target.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                        {
                            if (IsSteamAppInstalledFromUri(target))
                            {
                                game.Installed = true;
                            }
                        }
                        else if (File.Exists(target) || Directory.Exists(target))
                        {
                            game.Installed = true;
                        }
                    }
                }
                catch { }

                if (_userCategories.TryGetValue(game.Name, out var userCategory) && !string.IsNullOrWhiteSpace(userCategory))
                {
                    game.Category = userCategory;
                }
                else
                {
                    game.Category = string.IsNullOrWhiteSpace(metadata?.Category) ? "Uncategorized" : metadata.Category;
                }

                AddCategory(game.Category);
                // Start install detection asynchronously so UI stays responsive
                _ = CheckAndSetInstalledAsync(game);
            }
        }
        else
        {
            // No shortcut files found — try enumerating directories as game entries
            try
            {
                var dirs = Directory.GetDirectories(targetPath);
                foreach (var dir in dirs)
                {
                    var name = Path.GetFileName(dir) ?? dir;
                    var game = new GameData { Name = name, Path = dir, Category = "Uncategorized" };
                    Games.Add(game);

                    var metadata = await SteamGridService.GetGridUrlAsync(game.Name, CurrentSettings.SteamGridApiKey);
                    game.GridUrl = metadata?.GridUrl;

                    if (_userCategories.TryGetValue(game.Name, out var userCategory) && !string.IsNullOrWhiteSpace(userCategory))
                    {
                        game.Category = userCategory;
                    }
                    else
                    {
                        game.Category = string.IsNullOrWhiteSpace(metadata?.Category) ? "Uncategorized" : metadata.Category;
                    }

                    AddCategory(game.Category);
                    _ = CheckAndSetInstalledAsync(game);
                }
            }
            catch { /* ignore */ }
        }

        RefreshCategoryCollections();
        SelectedCategory = "All";
        RefreshFilteredGames();
        LoadRecents();
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var chars = name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    private static bool IsNameSimilar(string folderName, string gameName)
    {
        if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(gameName)) return false;

        var f = NormalizeName(folderName);
        var g = NormalizeName(gameName);
        if (string.IsNullOrEmpty(f) || string.IsNullOrEmpty(g)) return false;

        // Direct containment (covers many cases where folder has extra suffix/prefix)
        if (f.Contains(g) || g.Contains(f)) return true;

        // Token approach: require most tokens from game name to appear in folder name
        var tokens = System.Text.RegularExpressions.Regex.Split(g, "[^a-z0-9]+");
        tokens = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        if (tokens.Length == 0) return false;

        int matches = tokens.Count(t => f.Contains(t));
        // If there's only one token, require it to match; otherwise allow one missing token
        return matches >= Math.Max(1, tokens.Length - 1);
    }

    private static bool DirectoryHasExecutable(string dir, int maxDepth = 2)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;

            if (Directory.EnumerateFiles(dir, "*.exe", SearchOption.TopDirectoryOnly).Any())
                return true;

            if (maxDepth <= 0) return false;

            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (DirectoryHasExecutable(sub, maxDepth - 1))
                    return true;
            }
        }
        catch { /* ignore inaccessible folders */ }

        return false;
    }

    private async Task CheckAndSetInstalledAsync(GameData game)
    {
        try
        {
            var roots = new List<string>();
            // Include user-configured library path's parent as a possible install root
            try { if (!string.IsNullOrWhiteSpace(CurrentSettings.GameLibraryPath)) roots.Add(CurrentSettings.GameLibraryPath); } catch { }

            try { var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles); if (!string.IsNullOrWhiteSpace(pf)) roots.Add(pf); } catch { }
            try { var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86); if (!string.IsNullOrWhiteSpace(pf86)) roots.Add(pf86); } catch { }

            // Common Steam installs
            try { var steam1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common"); roots.Add(steam1); } catch { }
            try { var steam2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common"); roots.Add(steam2); } catch { }

            // Reuse the real Steam library folders discovered via the registry / libraryfolders.vdf
            // (covers games installed on other drives, not just the default Steam install)
            try
            {
                foreach (var steamapps in GetSteamAppsFolders())
                {
                    if (string.IsNullOrWhiteSpace(steamapps)) continue;
                    roots.Add(Path.Combine(steamapps, "common"));
                }
            }
            catch { }

            // Common non-Steam game library locations on every fixed drive
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    var root = drive.RootDirectory.FullName;
                    foreach (var folderName in new[] { "Games", "Game Library", "SteamLibrary", "Epic Games" })
                    {
                        roots.Add(Path.Combine(root, folderName));
                    }
                }
            }
            catch { }

            string gameName = game.Name ?? string.Empty;

            bool found = false;

            // Check real install records from Epic / Ubisoft / GOG / Microsoft Store first —
            // these are exact, so they catch games the folder-name scan below would miss
            // (different drive, non-standard folder name, etc.)
            // 1) Launcher-detection records (Epic/Ubisoft/GOG/MS Store)
            foreach (var (launcherName, launcherPath) in LauncherDetectionService.GetAllInstalledGames())
            {
                if (Directory.Exists(launcherPath) && IsNameSimilar(launcherName, gameName) && DirectoryHasExecutable(launcherPath))
                {
                    found = true;
                    break;
                }
            }

            foreach (var root in found ? Enumerable.Empty<string>() : roots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                if (!Directory.Exists(root)) continue;

                // First try top-level directories only for performance
                IEnumerable<string> subdirs;
                try
                {
                    subdirs = Directory.EnumerateDirectories(root);
                }
                catch { continue; }

                // 2) Top-level folder scan
                foreach (var dir in subdirs)
                {
                    var folder = Path.GetFileName(dir) ?? dir;
                    if (IsNameSimilar(folder, gameName) && DirectoryHasExecutable(dir))
                    {
                        found = true;
                        break;
                    }
                }

                if (found) break;

                // If not found, try one deeper level beneath top directories (covers nested folders)
                try
                {
                    foreach (var dir in subdirs)
                    {
                        if (!Directory.Exists(dir)) continue;
                        IEnumerable<string> inner;
                        try { inner = Directory.EnumerateDirectories(dir); } catch { continue; }
                        // 3) One-level-deeper nested folder scan
                            foreach (var innerDir in inner)
                            {
                                var folder = Path.GetFileName(innerDir) ?? innerDir;
                                if (IsNameSimilar(folder, gameName) && DirectoryHasExecutable(innerDir))
                                {
                                    found = true;
                                    break;
                                }
                            }
                        if (found) break;
                    }
                }
                catch { }

                if (found) break;
            }

            // Update property on UI thread.
            // Only ever upgrade Installed to true here — never downgrade it.
            // A prior, more reliable check (the shortcut's target actually
            // existing on disk) may have already set this to true, and this
            // fuzzy folder-name scan should not be allowed to undo that.
            if (found)
            {
                await Dispatcher.InvokeAsync(() => game.Installed = true);
            }
        }
        catch
        {
            // Ignore errors from scanning; leave Installed as it was
        }
    }

    private static string? ResolveShortcutTarget(string shortcutPath)
    {
        try
        {
            var ext = Path.GetExtension(shortcutPath) ?? string.Empty;
            if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic lnk = shell.CreateShortcut(shortcutPath);
                        string? target = lnk?.TargetPath as string;
                        string? args = lnk?.Arguments as string;

                        // If the shortcut launches Steam with -applaunch <id>, return a steam:// style uri
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(args))
                            {
                                var m = System.Text.RegularExpressions.Regex.Match(args, @"-applaunch\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                if (m.Success)
                                {
                                    var id = m.Groups[1].Value;
                                    try { Marshal.ReleaseComObject(lnk); } catch { }
                                    try { Marshal.ReleaseComObject(shell); } catch { }
                                    return $"steam://run/{id}";
                                }
                            }
                        }
                        catch { }

                        try { Marshal.ReleaseComObject(lnk); } catch { }
                        try { Marshal.ReleaseComObject(shell); } catch { }
                        // Prefer returning the target exe path when available
                        return target;
                    }
                }
            }

            if (string.Equals(ext, ".url", StringComparison.OrdinalIgnoreCase))
            {
                var lines = File.ReadAllLines(shortcutPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        var url = line.Substring(4).Trim();
                        return url;
                    }
                    if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        var icon = line.Substring(9).Trim();
                        if (!string.IsNullOrWhiteSpace(icon)) return icon;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static bool IsSteamAppInstalledFromUri(string uri)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uri)) return false;
            // Example patterns: steam://rungameid/252950 or steam://run/252950
            var m = System.Text.RegularExpressions.Regex.Match(uri, @"(?:rungameid|run)[/\\](\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return false;

            var appid = m.Groups[1].Value;
            var steamappsFolders = GetSteamAppsFolders();
            foreach (var steamapps in steamappsFolders)
            {
                if (string.IsNullOrWhiteSpace(steamapps)) continue;
                var manifest = Path.Combine(steamapps, $"appmanifest_{appid}.acf");
                if (File.Exists(manifest)) return true;
                // Also try fuzzy folder name match inside the 'common' folder
                try
                {
                    var common = Path.Combine(steamapps, "common");
                    if (Directory.Exists(common))
                    {
                        var dirs = Directory.EnumerateDirectories(common);
                        foreach (var dir in dirs)
                        {
                            var folder = Path.GetFileName(dir) ?? dir;
                            if (IsNameSimilar(folder, uri)) return true; // uri may contain app name in some formats
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private static List<string> GetSteamAppsFolders()
    {
        var resultsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Try HKCU first
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
                var steamPath = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrWhiteSpace(steamPath))
                {
                    var steamapps = Path.Combine(steamPath, "steamapps");
                    resultsSet.Add(steamapps);
                }
            }
            catch { }

            // Try HKLM as fallback
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\WOW6432Node\\Valve\\Steam");
                var steamPath = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(steamPath))
                {
                    var steamapps = Path.Combine(steamPath, "steamapps");
                    resultsSet.Add(steamapps);
                }
            }
            catch { }

            // Add common ProgramFiles locations
            try { var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles); if (!string.IsNullOrWhiteSpace(pf)) resultsSet.Add(Path.Combine(pf, "Steam", "steamapps")); } catch { }
            try { var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86); if (!string.IsNullOrWhiteSpace(pf86)) resultsSet.Add(Path.Combine(pf86, "Steam", "steamapps")); } catch { }
        }
        catch { }

        // Also parse libraryfolders.vdf in each steamapps folder to find additional libraries
        var toAdd = new List<string>();
        foreach (var steamapps in resultsSet.ToList())
        {
            try
            {
                var vdf = Path.Combine(steamapps, "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    var libs = ParseLibraryFoldersVdf(vdf);
                    foreach (var lib in libs)
                    {
                        try
                        {
                            var candidate = Path.Combine(lib, "steamapps");
                            toAdd.Add(candidate);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        foreach (var a in toAdd) resultsSet.Add(a);

        return resultsSet.ToList();
    }

    private static List<string> ParseLibraryFoldersVdf(string vdfPath)
    {
        var results = new List<string>();
        try
        {
            var text = File.ReadAllText(vdfPath);
            var matches = System.Text.RegularExpressions.Regex.Matches(text, "\\\"path\\\"\\s*\\\"([^\\\"]+)\\\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var p = m.Groups[1].Value.Replace("\\\\", "\\");
                if (!string.IsNullOrWhiteSpace(p)) results.Add(p);
            }
        }
        catch { }
        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is GameData game)
        {
            Launcher.Launch(game);
            AddToRecents(game);
        }
    }

    private void RecentsBlock_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (MainContentScrollViewer == null) return;

        e.Handled = true;
        var newOffset = MainContentScrollViewer.VerticalOffset - (e.Delta / 3.0);
        MainContentScrollViewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(MainContentScrollViewer.ScrollableHeight, newOffset)));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshFilteredGames();

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshFilteredGames();

    private void RefreshFilteredGames()
    {
        string query = SearchBox?.Text?.ToLower() ?? string.Empty;
        var filtered = Games.Where(g =>
            (SelectedCategory == "All" || string.Equals(g.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(query) || g.Name.ToLower().Contains(query)))
            .ToList();

        FilteredGames.Clear();
        foreach (var game in filtered)
        {
            FilteredGames.Add(game);
        }
    }

    private void AddCategory(string? category)
    {
        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCategory))
        {
            return;
        }

        AddCustomCategoryOption(normalizedCategory);

        if (Categories.Any(c => string.Equals(c, normalizedCategory, StringComparison.OrdinalIgnoreCase)))
        {
            RefreshCategoryCollections();
            return;
        }

        Categories.Add(normalizedCategory);
        RefreshCategoryCollections();
    }

    private void AddCustomCategoryOption(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return;
        }

        var normalized = category.Trim();
        if (string.Equals(normalized, "All", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _customCategoryOptions.Add(normalized);
    }

    private void RefreshCategoryCollections()
    {
        var usedCategories = Games
            .Select(g => g.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Where(c => !string.Equals(c, "All", StringComparison.OrdinalIgnoreCase))
            .Concat(_customCategoryOptions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilterCategories.Clear();
        FilterCategories.Add("All");
        foreach (var category in usedCategories)
        {
            FilterCategories.Add(category);
        }

        AssignableCategories.Clear();
        foreach (var category in usedCategories)
        {
            AssignableCategories.Add(category);
        }

        if (!string.IsNullOrWhiteSpace(_selectedCategory) &&
            !string.Equals(_selectedCategory, "All", StringComparison.OrdinalIgnoreCase) &&
            !FilterCategories.Any(c => string.Equals(c, _selectedCategory, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedCategory = "All";
            NotifyPropertyChanged(nameof(SelectedCategory));
        }
    }

    private void OpenCategoryManager_Click(object sender, RoutedEventArgs e)
    {
        var categoryManagerPanel = this.FindName("CategoryManagerPanel") as Border;
        if (categoryManagerPanel != null)
        {
            categoryManagerPanel.Visibility = categoryManagerPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private void CategorySelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is GameData game && comboBox.SelectedItem is string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || string.Equals(categoryName, "All", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            game.Category = categoryName;
            _userCategories[game.Name] = categoryName;
            AppSettings.SaveUserCategories(_userCategories);
            AddCategory(categoryName);
            RefreshFilteredGames();
        }
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var categoryName = NewCategoryBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            MessageBox.Show("Enter a category name first.", "Category Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AddCategory(categoryName);
        if (NewCategoryBox != null)
        {
            NewCategoryBox.Text = string.Empty;
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        InitializeSettingsUiFromCurrentSettings();
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        // Just hide the overlay and reload the settings from disk to discard changes
        CurrentSettings = AppSettings.Load();
        this.DataContext = null; // Reset context to refresh bindings
        this.DataContext = this;
        InitializeSettingsUiFromCurrentSettings();
        SettingsOverlay.Visibility = Visibility.Collapsed;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        // 1. Save the new values to the JSON file
        CurrentSettings.Save();
        
        // 2. Hide the overlay
        SettingsOverlay.Visibility = Visibility.Collapsed;

        // 3. Prompt the user
        var result = MessageBox.Show(
            "Settings saved! Some changes require a restart to take effect. Would you like to restart Paradise Launcher now?", 
            "Restart Required", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            // Get the path to the current executable
            var processPath = System.Environment.ProcessPath;
            
            if (processPath != null)
            {
                // Start a new instance
                System.Diagnostics.Process.Start(processPath);
            }

            // Shut down this instance
            Application.Current.Shutdown();
        }
    }

    private void BrowseGameLibraryPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the folder that contains your game shortcuts."
        };

        if (!string.IsNullOrWhiteSpace(CurrentSettings.GameLibraryPath) && Directory.Exists(CurrentSettings.GameLibraryPath))
        {
            dialog.InitialDirectory = CurrentSettings.GameLibraryPath;
        }

        if (dialog.ShowDialog() == true)
        {
            CurrentSettings.GameLibraryPath = dialog.FolderName;
            if (GameLibraryPathBox != null)
            {
                GameLibraryPathBox.Text = dialog.FolderName;
            }
        }
    }

    private void LibraryBannerScaleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LibraryBannerScaleBox?.SelectedItem is ComboBoxItem item && item.Tag is string scaleTag)
        {
            var (width, height) = ParseScaleTag(scaleTag);
            CurrentSettings.BannerWidth = width;
            CurrentSettings.BannerHeight = height;
            NotifyPropertyChanged(nameof(CurrentSettings));
        }
    }

    private void RecentsBannerScaleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentsBannerScaleBox?.SelectedItem is ComboBoxItem item && item.Tag is string scaleTag)
        {
            var (width, height) = ParseScaleTag(scaleTag);
            CurrentSettings.RecentsBannerWidth = width;
            CurrentSettings.RecentsBannerHeight = height;
            NotifyPropertyChanged(nameof(CurrentSettings));
        }
    }

    private static (int Width, int Height) ParseScaleTag(string tag)
    {
        var parts = tag.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return (150, 225);
        }

        if (!int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height))
        {
            return (150, 225);
        }

        return (width, height);
    }

    private void InitializeSettingsUiFromCurrentSettings()
    {
        _isUpdatingColorControls = true;
        try
        {
            SelectScaleComboItem(LibraryBannerScaleBox, (int)CurrentSettings.BannerWidth, (int)CurrentSettings.BannerHeight);
            SelectScaleComboItem(RecentsBannerScaleBox, (int)CurrentSettings.RecentsBannerWidth, (int)CurrentSettings.RecentsBannerHeight);
            if (ColorTargetBox != null)
            {
                ColorTargetBox.SelectedIndex = 0;
            }

            UpdateThemePreviewFromSettings();
            UpdateCurrentColorSwatches();
            LoadPickerFromTarget("Background");
        }
        finally
        {
            _isUpdatingColorControls = false;
        }
    }

    private static void SelectScaleComboItem(System.Windows.Controls.ComboBox? comboBox, int width, int height)
    {
        if (comboBox == null)
        {
            return;
        }

        var desired = $"{width}x{height}";
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), desired, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 1;
    }

    private void ColorTargetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingColorControls)
        {
            return;
        }

        var target = (ColorTargetBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Background";
        LoadPickerFromTarget(target);
    }

    private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingColorControls)
        {
            return;
        }

        _pickerHue = HueSlider.Value;
        UpdateSvBaseFromHue();
        UpdateColorFromPickerState();
    }

    private void SVCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSv = true;
        SVCanvas.CaptureMouse();
        UpdateSvFromPoint(e.GetPosition(SVCanvas));
    }

    private void SVCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSv)
        {
            return;
        }

        UpdateSvFromPoint(e.GetPosition(SVCanvas));
    }

    private void SVCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSv = false;
        SVCanvas.ReleaseMouseCapture();
    }

    private void SVCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSvSelectorPosition();
    }

    private void PickerHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingColorControls)
        {
            return;
        }

        var color = TryParseHexColor(PickerHexBox.Text);
        if (color == null)
        {
            return;
        }

        SetPickerStateFromColor(color.Value, true);
        UpdateColorFromPickerState();
    }

    private void LoadPickerFromTarget(string target)
    {
        _activeColorTarget = string.Equals(target, "Navbar", StringComparison.OrdinalIgnoreCase) ? "Navbar" : "Background";
        var hex = string.Equals(_activeColorTarget, "Navbar", StringComparison.OrdinalIgnoreCase)
            ? CurrentSettings.NavbarColor
            : CurrentSettings.BackgroundColor;
        var color = TryParseHexColor(hex) ?? System.Windows.Media.Color.FromRgb(30, 30, 30);
        SetPickerStateFromColor(color, true);
        UpdateSvBaseFromHue();
        UpdateColorReadoutsAndHex(color);
        UpdateSvSelectorPosition();
    }

    private void SetPickerStateFromColor(System.Windows.Media.Color color, bool updateHueSlider)
    {
        RgbToHsv(color.R, color.G, color.B, out var hue, out var saturation, out var value);
        _pickerHue = hue;
        _pickerSaturation = saturation;
        _pickerValue = value;

        if (updateHueSlider && HueSlider != null)
        {
            _isUpdatingColorControls = true;
            try
            {
                HueSlider.Value = hue;
            }
            finally
            {
                _isUpdatingColorControls = false;
            }
        }
    }

    private void UpdateSvFromPoint(Point point)
    {
        var width = Math.Max(1.0, SVCanvas.ActualWidth);
        var height = Math.Max(1.0, SVCanvas.ActualHeight);
        var x = Math.Max(0, Math.Min(width, point.X));
        var y = Math.Max(0, Math.Min(height, point.Y));

        _pickerSaturation = x / width;
        _pickerValue = 1.0 - (y / height);
        UpdateColorFromPickerState();
    }

    private void UpdateColorFromPickerState()
    {
        var color = HsvToRgb(_pickerHue, _pickerSaturation, _pickerValue);
        var hex = ToHex(color);

        if (string.Equals(_activeColorTarget, "Navbar", StringComparison.OrdinalIgnoreCase))
        {
            CurrentSettings.NavbarColor = hex;
        }
        else
        {
            CurrentSettings.BackgroundColor = hex;
        }

        UpdateThemePreviewFromSettings();
        UpdateCurrentColorSwatches();
        UpdateColorReadoutsAndHex(color);
        UpdateSvSelectorPosition();
        NotifyPropertyChanged(nameof(CurrentSettings));
    }

    private void UpdateThemePreviewFromSettings()
    {
        var bg = TryParseHexColor(CurrentSettings.BackgroundColor) ?? System.Windows.Media.Color.FromRgb(18, 18, 18);
        var nav = TryParseHexColor(CurrentSettings.NavbarColor) ?? System.Windows.Media.Color.FromRgb(30, 30, 30);
        this.Background = new SolidColorBrush(bg);
        if (TopNavbarBorder != null)
        {
            TopNavbarBorder.Background = new SolidColorBrush(nav);
        }
        if (SettingsPanelBorder != null)
        {
            SettingsPanelBorder.Background = new SolidColorBrush(nav);
        }
    }

    private void UpdateCurrentColorSwatches()
    {
        var bg = TryParseHexColor(CurrentSettings.BackgroundColor) ?? System.Windows.Media.Color.FromRgb(18, 18, 18);
        var nav = TryParseHexColor(CurrentSettings.NavbarColor) ?? System.Windows.Media.Color.FromRgb(30, 30, 30);

        if (BackgroundCurrentPreview != null)
        {
            BackgroundCurrentPreview.Background = new SolidColorBrush(bg);
        }
        if (NavbarCurrentPreview != null)
        {
            NavbarCurrentPreview.Background = new SolidColorBrush(nav);
        }
    }

    private void UpdateSvBaseFromHue()
    {
        var hueColor = HsvToRgb(_pickerHue, 1, 1);
        if (SVHueBase != null)
        {
            SVHueBase.Fill = new SolidColorBrush(hueColor);
        }
    }

    private void UpdateSvSelectorPosition()
    {
        if (SVCanvas == null || SVSelector == null)
        {
            return;
        }

        var width = Math.Max(1.0, SVCanvas.ActualWidth);
        var height = Math.Max(1.0, SVCanvas.ActualHeight);
        var x = _pickerSaturation * width;
        var y = (1.0 - _pickerValue) * height;

        Canvas.SetLeft(SVSelector, Math.Max(0, Math.Min(width - SVSelector.Width, x - (SVSelector.Width / 2))));
        Canvas.SetTop(SVSelector, Math.Max(0, Math.Min(height - SVSelector.Height, y - (SVSelector.Height / 2))));
    }

    private void UpdateColorReadoutsAndHex(System.Windows.Media.Color color)
    {
        var hex = ToHex(color);

        _isUpdatingColorControls = true;
        try
        {
            if (PickerHexBox != null)
            {
                PickerHexBox.Text = hex;
            }
        }
        finally
        {
            _isUpdatingColorControls = false;
        }

        if (RgbValueText != null)
        {
            RgbValueText.Text = $"{color.R}, {color.G}, {color.B}";
        }
        if (CmykValueText != null)
        {
            var (c, m, y, k) = RgbToCmyk(color.R, color.G, color.B);
            CmykValueText.Text = $"{c}%, {m}%, {y}%, {k}%";
        }
        if (HsvValueText != null)
        {
            HsvValueText.Text = $"{Math.Round(_pickerHue)}deg, {Math.Round(_pickerSaturation * 100)}%, {Math.Round(_pickerValue * 100)}%";
        }
        if (HslValueText != null)
        {
            var (h, s, l) = RgbToHsl(color.R, color.G, color.B);
            HslValueText.Text = $"{Math.Round(h)}deg, {Math.Round(s * 100)}%, {Math.Round(l * 100)}%";
        }
    }

    private static string ToHex(System.Windows.Media.Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static (int C, int M, int Y, int K) RgbToCmyk(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;
        double k = 1 - Math.Max(rd, Math.Max(gd, bd));

        if (Math.Abs(k - 1.0) < 0.00001)
        {
            return (0, 0, 0, 100);
        }

        double c = (1 - rd - k) / (1 - k);
        double m = (1 - gd - k) / (1 - k);
        double y = (1 - bd - k) / (1 - k);
        return ((int)Math.Round(c * 100), (int)Math.Round(m * 100), (int)Math.Round(y * 100), (int)Math.Round(k * 100));
    }

    private static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double h;
        double s;
        double l = (max + min) / 2;

        if (Math.Abs(max - min) < 0.00001)
        {
            h = 0;
            s = 0;
        }
        else
        {
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

            if (Math.Abs(max - rd) < 0.00001)
            {
                h = (gd - bd) / d + (gd < bd ? 6 : 0);
            }
            else if (Math.Abs(max - gd) < 0.00001)
            {
                h = (bd - rd) / d + 2;
            }
            else
            {
                h = (rd - gd) / d + 4;
            }

            h *= 60;
        }

        return (h, s, l);
    }

    private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;

        if (Math.Abs(delta) < 0.00001)
        {
            h = 0;
        }
        else if (Math.Abs(max - rd) < 0.00001)
        {
            h = 60 * (((gd - bd) / delta) % 6);
        }
        else if (Math.Abs(max - gd) < 0.00001)
        {
            h = 60 * (((bd - rd) / delta) + 2);
        }
        else
        {
            h = 60 * (((rd - gd) / delta) + 4);
        }

        if (h < 0)
        {
            h += 360;
        }

        s = Math.Abs(max) < 0.00001 ? 0 : delta / max;
        v = max;
    }

    private static System.Windows.Media.Color HsvToRgb(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0 % 2) - 1));
        double m = v - c;

        double r1;
        double g1;
        double b1;

        if (h < 60)
        {
            r1 = c; g1 = x; b1 = 0;
        }
        else if (h < 120)
        {
            r1 = x; g1 = c; b1 = 0;
        }
        else if (h < 180)
        {
            r1 = 0; g1 = c; b1 = x;
        }
        else if (h < 240)
        {
            r1 = 0; g1 = x; b1 = c;
        }
        else if (h < 300)
        {
            r1 = x; g1 = 0; b1 = c;
        }
        else
        {
            r1 = c; g1 = 0; b1 = x;
        }

        return System.Windows.Media.Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    private static System.Windows.Media.Color? TryParseHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var value = hex.Trim();
        if (!value.StartsWith('#'))
        {
            value = $"#{value}";
        }

        if (value.Length != 7)
        {
            return null;
        }

        try
        {
            var r = Convert.ToByte(value.Substring(1, 2), 16);
            var g = Convert.ToByte(value.Substring(3, 2), 16);
            var b = Convert.ToByte(value.Substring(5, 2), 16);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }
        catch
        {
            return null;
        }
    }

    private void LoadRecents()
    {
        try
        {
            if (File.Exists(RecentsPath))
            {
                string json = File.ReadAllText(RecentsPath);
                var savedNames = JsonSerializer.Deserialize<List<string>>(json);
                
                if (savedNames != null)
                {
                    RecentGames.Clear();
                    foreach (var name in savedNames)
                    {
                        var matchingGame = Games.FirstOrDefault(g => g.Name == name);
                        if (matchingGame != null) RecentGames.Add(matchingGame);
                    }
                }
            }
        }
        catch { /* Handle error */ }
    }

    private void AddToRecents(GameData game)
    {
        if (RecentGames.Contains(game)) RecentGames.Remove(game);
        RecentGames.Insert(0, game);
        if (RecentGames.Count > 10) RecentGames.RemoveAt(10);

        var names = RecentGames.Select(g => g.Name).ToList();
        File.WriteAllText(RecentsPath, JsonSerializer.Serialize(names));
    }

    // INotifyPropertyChanged Implementation
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void NotifyPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}