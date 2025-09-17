using Archivist.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Archivist.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial string VaultPath { get; set; } = "Папка не выбрана";
        [ObservableProperty]
        public partial string SubDirectoryPath { get; set; } = "Папка не выбрана";
        [ObservableProperty]
        public partial string FilenameFormat { get; set; } = "Сессия №{number}. {name}";
        [ObservableProperty]
        public partial Visibility FilenameFormatErrorVisibility { get; set; } = Visibility.Collapsed;
        [ObservableProperty]
        public partial Visibility SaveNotificationVisibility { get; set; } = Visibility.Collapsed;

        private AppConfig _config;
        private readonly char[] _bannedChars = new[] { '*', '"', '\\', '/', ':', '?', '<', '>', '|' };
        private Timer _saveTimer;
        private DispatcherQueue _dispatcherQueue;
        private Action _showSaveNotificationAction;

        public SettingsPageViewModel(DispatcherQueue dispatcherQueue, Action showSaveNotificationAction)
        {
            _dispatcherQueue = dispatcherQueue;
            _showSaveNotificationAction = showSaveNotificationAction;
            _config = AppConfig.Load();

            if (_config.Vault != string.Empty)
            {
                VaultPath = _config.Vault;
            }

            if (_config.SubDirectory != string.Empty)
            {
                SubDirectoryPath = _config.SubDirectory;
            }

            if (_config.Format != string.Empty)
            {
                FilenameFormat = _config.Format;
            }

            _saveTimer = new Timer(800);
            _saveTimer.Elapsed += SaveTimer_Elapsed;
            _saveTimer.AutoReset = false;

        }

        partial void OnFilenameFormatChanged(string value)
        {
            if (value.IndexOfAny(_bannedChars) >= 0)
            {
                FilenameFormatErrorVisibility = Visibility.Visible;
            }
            else
            {
                FilenameFormatErrorVisibility = Visibility.Collapsed;

                _saveTimer.Stop();
                _saveTimer.Start();
            }
        }

        [RelayCommand]
        private async Task SelectFolderAsync()
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.ViewMode = PickerViewMode.List;

            var hwnd = GetWindowHandle();
            if (hwnd == IntPtr.Zero)
            {
                VaultPath = "Error: Main window not found.";
                return;
            }

            InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null && Directory.EnumerateDirectories(folder.Path, ".obsidian").Any())
            {
                VaultPath = folder.Path;
                _config.Vault = folder.Path;
                await _config.SaveAsync();
            }
            else
            {
                await ShowErrorDialogAsync("Ошибка", "Выберите директорию, являющуюся хранилищем Obsidian");
            }
        }

        [RelayCommand]
        private async Task SelectSubFolderAsync()
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.ViewMode = PickerViewMode.List;

            var hwnd = GetWindowHandle();
            if (hwnd == IntPtr.Zero)
            {
                SubDirectoryPath = "Error: Main window not found.";
                return;
            }

            InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null && folder.Path.StartsWith(VaultPath))
            {
                SubDirectoryPath = folder.Path;
                _config.SubDirectory = folder.Path;
                await _config.SaveAsync();
            }
            else
            {
                await ShowErrorDialogAsync("Ошибка", $"Выберите директорию внутри хранилища Obsidian {VaultPath}");
            }
        }

        private async void SaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            _config.Format = FilenameFormat;
            await _config.SaveAsync();
            _dispatcherQueue.TryEnqueue(() => _showSaveNotificationAction?.Invoke());
        }

        private IntPtr GetWindowHandle()
        {
            Window? mainWindow = (Application.Current as App)?.MainWindow;
            return mainWindow != null ? WindowNative.GetWindowHandle(mainWindow) : IntPtr.Zero;
        }

        private Task ShowErrorDialogAsync(string title, string content)
        {
            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            });
            return Task.CompletedTask;
        }
    }
}