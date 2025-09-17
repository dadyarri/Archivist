using Archivist.Models;
using Archivist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

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
        public partial string PythonExecutablePath { get; set; } = string.Empty;
        [ObservableProperty]
        public partial string PythonScriptPath { get; set; } = string.Empty;
        [ObservableProperty]
        public partial Visibility FilenameFormatErrorVisibility { get; set; } = Visibility.Collapsed;
        [ObservableProperty]
        public partial Visibility SaveNotificationVisibility { get; set; } = Visibility.Collapsed;

        private AppConfig _config;
        private readonly char[] _bannedChars = ['*', '"', '\\', '/', ':', '?', '<', '>', '|'];
        private Timer _saveTimer;
        private DispatcherQueue _dispatcherQueue;
        private Action _showSaveNotificationAction;
        private IFilePickerService _filePicker;
        private IDialogService _dialog;

        public SettingsPageViewModel(DispatcherQueue dispatcherQueue, Action showSaveNotificationAction, IFilePickerService filePicker, IDialogService dialog)
        {
            _dispatcherQueue = dispatcherQueue;
            _showSaveNotificationAction = showSaveNotificationAction;
            _config = AppConfig.Load();

            _filePicker = filePicker;
            _dialog = dialog;

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

            if (_config.PythonExecutable != string.Empty)
            {
                PythonExecutablePath = _config.PythonExecutable;
            }

            if (_config.PythonScriptPath != string.Empty)
            {
                PythonScriptPath = _config.PythonScriptPath;
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
            var folder = await _filePicker.PickFolderAsync();
            if (folder == null)
            {
                return;
            }

            if (Directory.EnumerateDirectories(folder.Path, ".obsidian").Any())
            {
                VaultPath = folder.Path;
                _config.Vault = folder.Path;
                await _config.SaveAsync();
            }
            else
            {
                await _dialog.ShowDialogAsync("Ошибка", "Выберите директорию, являющуюся хранилищем Obsidian");
            }
        }

        [RelayCommand]
        private async Task SelectSubFolderAsync()
        {
            var folder = await _filePicker.PickFolderAsync();
            if (folder == null)
            {
                return;
            }

            if (folder.Path.StartsWith(VaultPath))
            {
                SubDirectoryPath = folder.Path;
                _config.SubDirectory = folder.Path;
                await _config.SaveAsync();
            }
            else
            {
                await _dialog.ShowDialogAsync("Ошибка", $"Выберите директорию внутри хранилища Obsidian {VaultPath}");
            }
        }

        [RelayCommand]
        private async Task SelectPythonExecutableAsync()
        {
            var file = await _filePicker.PickFileAsync();
            if (file == null) { return; }

            if ((Path.GetExtension(file.Path) == "exe" && OperatingSystem.IsWindows()) ||
                ((Path.GetExtension(file.Path) == string.Empty && OperatingSystem.IsLinux())))
            {
                PythonExecutablePath = file.Path;
                _config.PythonExecutable = file.Path;
                await _config.SaveAsync();
            }
            else
            {
                await _dialog.ShowDialogAsync("Ошибка", $"Выберите корректный исполняемый файл");
            }
        }

        [RelayCommand]
        private async Task SelectPythonScriptAsync()
        {
            var file = await _filePicker.PickFileAsync();
            if (file == null) { return; }


            if (Path.GetExtension(file.Path) != "py")
            {
                PythonScriptPath = file.Path;
                _config.PythonScriptPath = file.Path;
                await _config.SaveAsync();
            }
            else
            {
                await _dialog.ShowDialogAsync("Ошибка", $"Выберите корректный файл Python-скрипта");
            }
        }

        private async void SaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            _config.Format = FilenameFormat;
            await _config.SaveAsync();
            _dispatcherQueue.TryEnqueue(() => _showSaveNotificationAction?.Invoke());
        }
    }
}