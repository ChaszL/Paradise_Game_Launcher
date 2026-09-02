using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameLauncher;

public class GameData : INotifyPropertyChanged
{
    public required string Name { get; set; }
    public required string Path { get; set; }

    private string? _gridUrl;
    public string? GridUrl 
    { 
        get => _gridUrl; 
        set 
        { 
            _gridUrl = value; 
            // This tells the WPF UI to re-draw the image for this game
            OnPropertyChanged(); 
        } 
    }

    private string _category = "Uncategorized";
    public string Category
    {
        get => _category;
        set
        {
            if (_category != value)
            {
                _category = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _installed;
    public bool Installed
    {
        get => _installed;
        set
        {
            if (_installed != value)
            {
                _installed = value;
                OnPropertyChanged();
            }
        }
    }

    // Boilerplate code required for WPF property updates
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public static class GameFactory
{
    public static GameData CreateFromPath(string filePath)
    {
        return new GameData 
        { 
            Name = Path.GetFileNameWithoutExtension(filePath), 
            Path = filePath,
            Category = "Uncategorized"
        };
    }
}