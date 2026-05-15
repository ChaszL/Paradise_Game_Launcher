using System;
using System.Diagnostics;
using System.Windows; // Required for MessageBox

namespace GameLauncher
{
    public class Launcher
    {
        public static void Launch(GameData game)
        {
            try
            {
                // start the process using the game's file path
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = game.Path,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                // Print any errors that occur during the launch process to a GUI popup
                MessageBox.Show($"Error launching {game.Name}:\n{ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}