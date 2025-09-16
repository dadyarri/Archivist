using Archivist.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.IO;
using System.Linq;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Archivist.Views
{
    /// <summary>
    /// Page for application settings.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public AppConfig _config { get; set; }
        private DispatcherTimer _saveTimer;
        private readonly char[] _bannedChars = new[] { '*', '"', '\\', '/', ':', '?', '<', '>', '|' };

        public SettingsPage()
        {
            InitializeComponent();
            _config = AppConfig.Load();
            if (_config.Vault != string.Empty)
            {
                SelectedFolderText.Text = _config.Vault;
            }

            if (_config.SubDirectory != string.Empty)
            {
                SelectedSubFolderText.Text = _config.SubDirectory;
            }

            if (_config.Format != string.Empty)
            {
                FilenameFormatInput.Text = _config.Format;
            }

            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            _saveTimer.Tick += SaveTimer_Tick;
        }

        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.ViewMode = PickerViewMode.List;

            Window? mainWindow = (Application.Current as App)?.MainWindow;
            if (mainWindow == null)
            {
                SelectedFolderText.Text = "Error: Main window not found.";
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(mainWindow);
            InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null && Directory.EnumerateDirectories(folder.Path, ".obsidian").Any())
            {
                SelectedFolderText.Text = folder.Path;
                _config.Vault = folder.Path;
                await _config.SaveAsync();
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = $"Выберите директорию, являющуюся хранилищем Obsidian",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private async void SelectSubFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.ViewMode = PickerViewMode.List;

            Window? mainWindow = (Application.Current as App)?.MainWindow;
            if (mainWindow == null)
            {
                SelectedFolderText.Text = "Error: Main window not found.";
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(mainWindow);
            InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null && folder.Path.StartsWith(SelectedFolderText.Text))
            {
                SelectedSubFolderText.Text = folder.Path;
                _config.SubDirectory = folder.Path;
                await _config.SaveAsync();
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = $"Выберите директорию внутри хранилища Obsidian {SelectedFolderText.Text}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void FilenameFormatInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = FilenameFormatInput.Text;

            if (text.IndexOfAny(_bannedChars) >= 0)
            {
                FilenameFormatError.Visibility = Visibility.Visible;
            }
            else
            {
                FilenameFormatError.Visibility = Visibility.Collapsed;

                _saveTimer.Stop();
                _saveTimer.Start();
            }
        }

        private async void SaveTimer_Tick(object? sender, object e)
        {
            _saveTimer.Stop();

            // Save the current value to your config
            _config.Format = FilenameFormatInput.Text;
            await _config.SaveAsync();
            ShowSaveNotification();
        }


        private async void ShowSaveNotification(int milliseconds = 1500)
        {
            SaveNotification.Opacity = 0;
            SaveNotification.Visibility = Visibility.Visible;

            // Fade in
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            var sbIn = new Storyboard();
            Storyboard.SetTarget(fadeIn, SaveNotification);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            sbIn.Children.Add(fadeIn);
            sbIn.Begin();

            // Wait for display duration
            await Task.Delay(milliseconds);

            // Fade out
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            var sbOut = new Storyboard();
            Storyboard.SetTarget(fadeOut, SaveNotification);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");
            sbOut.Children.Add(fadeOut);

            sbOut.Completed += (s, e) =>
            {
                SaveNotification.Visibility = Visibility.Collapsed;
            };
            sbOut.Begin();
        }
    }
}