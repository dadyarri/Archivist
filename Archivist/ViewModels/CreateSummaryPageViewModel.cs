using Archivist.Models;
using Archivist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Archivist.ViewModels
{
    public partial class CreateSummaryViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        public partial string SelectedFolderText { get; set; } = "Папка не выбрана";

        [ObservableProperty]
        public partial Visibility Step2Visibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial string NoFilesText { get; set; } = "Сначала выберите папку с аудиофайлами";

        [ObservableProperty]
        public partial Visibility NoFilesVisibility { get; set; } = Visibility.Visible;

        [ObservableProperty]
        public partial Visibility AudioFilesVisibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial bool CreateSummaryEnabled { get; set; } = false;

        [ObservableProperty]
        public partial Visibility StepsVisibility { get; set; } = Visibility.Visible;

        [ObservableProperty]
        public partial Visibility ProgressVisibility { get; set; } = Visibility.Collapsed;

        [ObservableProperty]
        public partial Visibility CreateSummaryVisibility { get; set; } = Visibility.Visible;

        [ObservableProperty]
        public partial string ProgressStageTitle { get; set; } = "Создание саммари...";

        [ObservableProperty]
        public partial string ProgressText { get; set; } = "Подготовка...";

        [ObservableProperty]
        public partial double ProgressValue { get; set; } = 0;

        public ObservableCollection<AudioFileItem> AudioFiles { get; } = new();

        private PythonService _pythonService;
        private AppConfig _appConfig;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly IFilePickerService _filePickerService;
        private readonly IDialogService _dialogService;

        public CreateSummaryViewModel(DispatcherQueue dispatcherQueue, IFilePickerService filePickerService, IDialogService dialogService)
        {
            _dispatcherQueue = dispatcherQueue;
            _filePickerService = filePickerService;
            _dialogService = dialogService;

            _pythonService = new PythonService();
            _appConfig = AppConfig.Load();

            SetupPythonServiceEvents();
        }

        private void SetupPythonServiceEvents()
        {
            _pythonService.ProgressReceived += OnProgressReceived;
            _pythonService.ResultReceived += OnResultReceived;
            _pythonService.ErrorReceived += OnErrorReceived;
            _pythonService.ServerLogReceived += OnServerLogReceived;
        }

        private void OnProgressReceived(ProgressMessage message)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                ProgressStageTitle = message.Stage;
                ProgressText = message.Message;
                ProgressValue = message.Percentage;
            });
        }

        private void OnResultReceived(ProgressMessage message)
        {
            _dispatcherQueue.TryEnqueue(async () =>
            {
                StepsVisibility = Visibility.Visible;
                ProgressVisibility = Visibility.Collapsed;
                CreateSummaryVisibility = Visibility.Visible;
                CreateSummaryEnabled = true;

                await _dialogService.ShowDialogAsync("Готово", $"Саммари успешно создано!\nРезультат: {message.Message}");
            });
        }

        private void OnErrorReceived(ProgressMessage message)
        {
            _dispatcherQueue.TryEnqueue(async () =>
            {
                StepsVisibility = Visibility.Visible;
                ProgressVisibility = Visibility.Collapsed;
                CreateSummaryEnabled = true;

                await _dialogService.ShowDialogAsync("Ошибка", $"Произошла ошибка при обработке: {message.Message}");
            });
        }

        private void OnServerLogReceived(string log)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                System.Diagnostics.Debug.WriteLine($"Python Service: {log}");
            });
        }

        [RelayCommand]
        private async Task SelectFolderAsync()
        {
            var folder = await _filePickerService.PickFolderAsync();
            if (folder != null)
            {
                SelectedFolderText = folder.Path;
                await LoadAudioFilesAsync(folder);
            }
        }

        private async Task LoadAudioFilesAsync(StorageFolder folder)
        {
            AudioFiles.Clear();

            try
            {
                var files = await folder.GetFilesAsync();
                var audioExtensions = new[] { ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".wma" };

                var audioFiles = files.Where(f => audioExtensions.Contains(f.FileType.ToLower())).ToList();

                if (audioFiles.Any())
                {
                    foreach (var file in audioFiles)
                    {
                        var audioFileItem = new AudioFileItem
                        {
                            FileName = file.Name,
                            FilePath = file.Path,
                            CharacterName = FilenameRegex().Match(file.Name).Groups[1].Value,
                        };
                        AudioFiles.Add(audioFileItem);
                    }

                    Step2Visibility = Visibility.Visible;
                    NoFilesVisibility = Visibility.Collapsed;
                    AudioFilesVisibility = Visibility.Visible;
                    CreateSummaryEnabled = true;
                }
                else
                {
                    Step2Visibility = Visibility.Collapsed;
                    NoFilesText = "В выбранной папке не найдено аудиофайлов";
                    NoFilesVisibility = Visibility.Visible;
                    AudioFilesVisibility = Visibility.Collapsed;
                    CreateSummaryEnabled = false;
                }
            }
            catch (Exception ex)
            {
                NoFilesText = $"Ошибка при загрузке файлов: {ex.Message}";
                NoFilesVisibility = Visibility.Visible;
                AudioFilesVisibility = Visibility.Collapsed;
                CreateSummaryEnabled = false;
            }
        }

        [RelayCommand]
        private void RemoveFile(AudioFileItem audioFile)
        {
            if (audioFile != null)
            {
                AudioFiles.Remove(audioFile);
            }

            if (!AudioFiles.Any())
            {
                NoFilesText = "Все файлы удалены. Выберите другую папку.";
                NoFilesVisibility = Visibility.Visible;
                AudioFilesVisibility = Visibility.Collapsed;
                CreateSummaryEnabled = false;
            }
        }

        [RelayCommand]
        private async Task CreateSummaryAsync()
        {
            var includedFiles = AudioFiles.Where(f => !string.IsNullOrWhiteSpace(f.CharacterName)).ToList();

            if (!includedFiles.Any())
            {
                await _dialogService.ShowDialogAsync("Внимание", "Выберите хотя бы один файл с указанным именем персонажа для создания саммари.");
                return;
            }

            StepsVisibility = Visibility.Collapsed;
            ProgressVisibility = Visibility.Visible;
            CreateSummaryVisibility = Visibility.Collapsed;
            CreateSummaryEnabled = false;
            _cancellationTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () => await ProcessWithPythonService(includedFiles, _cancellationTokenSource.Token));
        }

        private async Task ProcessWithPythonService(List<AudioFileItem> files, CancellationToken cancellationToken)
        {
            try
            {
                _pythonService.StartPythonService(_appConfig.PythonExecutable, _appConfig.PythonScriptPath);

                var isRunning = await _pythonService.PingPythonService();
                if (!isRunning)
                {
                    _dispatcherQueue.TryEnqueue(async () =>
                    {
                        StepsVisibility = Visibility.Visible;
                        ProgressVisibility = Visibility.Collapsed;
                        CreateSummaryEnabled = true;

                        await _dialogService.ShowDialogAsync("Ошибка", "Не удалось подключиться к Python сервису. Убедитесь, что Python скрипт запущен.");
                    });
                    return;
                }

                var fileData = files.Select(f => new FileData
                {
                    Path = f.FilePath!,
                    Character = f.CharacterName!
                }).ToList();

                var vault = _appConfig.Vault;
                var subdirectory = _appConfig.SubDirectory;
                var format = _appConfig.Format;

                var success = await _pythonService.StartProcessing(fileData, vault, subdirectory, format);

                if (!success)
                {
                    _dispatcherQueue.TryEnqueue(async () =>
                    {
                        StepsVisibility = Visibility.Visible;
                        ProgressVisibility = Visibility.Collapsed;
                        CreateSummaryEnabled = true;

                        await _dialogService.ShowDialogAsync("Ошибка", "Не удалось запустить обработку файлов.");
                    });
                }
            }
            catch (OperationCanceledException)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    StepsVisibility = Visibility.Visible;
                    ProgressVisibility = Visibility.Collapsed;
                    this.CreateSummaryEnabled = true;
                });
            }
            catch (Exception ex)
            {
                _dispatcherQueue.TryEnqueue(async () =>
                {
                    StepsVisibility = Visibility.Visible;
                    ProgressVisibility = Visibility.Collapsed;
                    CreateSummaryEnabled = true;

                    await _dialogService.ShowDialogAsync("Ошибка", $"Произошла ошибка: {ex.Message}");
                });
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
            _pythonService?.StopPythonService();

            StepsVisibility = Visibility.Visible;
            ProgressVisibility = Visibility.Collapsed;
            CreateSummaryEnabled = true;
        }

        public void Dispose()
        {
            _pythonService?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        [GeneratedRegex(@"\d+-(\S*)\..*")]
        private static partial Regex FilenameRegex();
    }
}