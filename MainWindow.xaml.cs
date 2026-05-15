using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameLauncher;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public AppSettings CurrentSettings { get; set; }
    public ObservableCollection<GameData> Games { get; set; } = new();
    public ObservableCollection<GameData> RecentGames { get; set; } = new();

    private HardwareMonitorService _monitor = new();
    private HardwareInfo _stats = new();

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
        
        GameItemsControl.ItemsSource = Games;
        _ = LoadGamesAsync();
        StartHardwarePolling();
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

        var files = Directory.GetFiles(targetPath).Where(f => f.EndsWith(".lnk") || f.EndsWith(".url"));

        foreach (var file in files)
        {
            var game = GameFactory.CreateFromPath(file);
            Games.Add(game);
            
            game.GridUrl = await SteamGridService.GetGridUrlAsync(game.Name, CurrentSettings.SteamGridApiKey)
                           ?? "pack://application:,,,/Assets/default_cover.png";
        }

        LoadRecents();
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is GameData game)
        {
            Launcher.Launch(game);
            AddToRecents(game);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text.ToLower();
        var filtered = Games.Where(g => g.Name.ToLower().Contains(query)).ToList();
        GameItemsControl.ItemsSource = filtered;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Visible;
    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        // Just hide the overlay and reload the settings from disk to discard changes
        CurrentSettings = AppSettings.Load();
        this.DataContext = null; // Reset context to refresh bindings
        this.DataContext = this;
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