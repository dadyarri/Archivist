using Archivist.Models;
using Archivist.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Archivist.Views
{
    /// <summary>
    /// Page for creating new summaries.
    /// </summary>
    public sealed partial class CreateSummaryPage : Page
    {
        private ObservableCollection<AudioFileItem> _audioFiles;
        private List<AudioFileItem> _remainingFiles; // Store remaining files in memory
        private CancellationTokenSource _cancellationTokenSource;
        private PythonService _pythonService;
        private AppConfig _appConfig;

        public CreateSummaryPage()
        {
            InitializeComponent();
            _audioFiles = new ObservableCollection<AudioFileItem>();
            _remainingFiles = new List<AudioFileItem>();
            AudioFilesListView.ItemsSource = _audioFiles;
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
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                ProgressStageTitle.Text = message.Stage;
                ProgressText.Text = message.Message;
                ProgressBar.Value = message.Percentage;
            });
        }

        private void OnResultReceived(ProgressMessage message)
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                // Show steps back and hide progress
                StepsContainer.Visibility = Visibility.Visible;
                ProgressBorder.Visibility = Visibility.Collapsed;
                CreateSummaryButton.Visibility = Visibility.Visible;
                CreateSummaryButton.IsEnabled = true;

                var dialog = new ContentDialog
                {
                    Title = "Готово",
                    Content = $"Саммари успешно создано!\nРезультат: {message.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            });
        }

        private void OnErrorReceived(ProgressMessage message)
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                // Show steps back and hide progress
                StepsContainer.Visibility = Visibility.Visible;
                ProgressBorder.Visibility = Visibility.Collapsed;
                CreateSummaryButton.IsEnabled = true;

                var dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = $"Произошла ошибка при обработке: {message.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            });
        }

        private void OnServerLogReceived(string log)
        {
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                // You can add logging to a text block or console if needed
                System.Diagnostics.Debug.WriteLine($"Python Service: {log}");
            });
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
            if (folder != null)
            {
                SelectedFolderText.Text = folder.Path;
                await LoadAudioFiles(folder);
            }
        }

        private async Task LoadAudioFiles(StorageFolder folder)
        {
            _audioFiles.Clear();
            _remainingFiles.Clear();

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
                        _audioFiles.Add(audioFileItem);
                        _remainingFiles.Add(audioFileItem);
                    }

                    // Show step 2 when files are loaded
                    Step2Border.Visibility = Visibility.Visible;
                    NoFilesText.Visibility = Visibility.Collapsed;
                    AudioFilesListView.Visibility = Visibility.Visible;
                    CreateSummaryButton.IsEnabled = true;
                }
                else
                {
                    // Hide step 2 when no files found
                    Step2Border.Visibility = Visibility.Collapsed;
                    NoFilesText.Text = "В выбранной папке не найдено аудиофайлов";
                    NoFilesText.Visibility = Visibility.Visible;
                    AudioFilesListView.Visibility = Visibility.Collapsed;
                    CreateSummaryButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                NoFilesText.Text = $"Ошибка при загрузке файлов: {ex.Message}";
                NoFilesText.Visibility = Visibility.Visible;
                AudioFilesListView.Visibility = Visibility.Collapsed;
                CreateSummaryButton.IsEnabled = false;
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is AudioFileItem audioFile)
            {
                // Remove from both collections
                _audioFiles.Remove(audioFile);
                _remainingFiles.Remove(audioFile);

                // Update button state if no files remain
                if (!_audioFiles.Any())
                {
                    NoFilesText.Text = "Все файлы удалены. Выберите другую папку.";
                    NoFilesText.Visibility = Visibility.Visible;
                    AudioFilesListView.Visibility = Visibility.Collapsed;
                    CreateSummaryButton.IsEnabled = false;
                }
            }
        }

        private async void CreateSummaryButton_Click(object sender, RoutedEventArgs e)
        {
            var includedFiles = _remainingFiles.Where(f => !string.IsNullOrWhiteSpace(f.CharacterName)).ToList();

            if (!includedFiles.Any())
            {
                var dialog = new ContentDialog
                {
                    Title = "Внимание",
                    Content = "Выберите хотя бы один файл с указанным именем персонажа для создания саммари.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            // Hide steps and show progress
            StepsContainer.Visibility = Visibility.Collapsed;
            ProgressBorder.Visibility = Visibility.Visible;
            CreateSummaryButton.Visibility = Visibility.Collapsed;
            CreateSummaryButton.IsEnabled = false;
            _cancellationTokenSource = new CancellationTokenSource();

            // Запускаем реальную обработку через Python сервис
            _ = Task.Run(async () => await ProcessWithPythonService(includedFiles, _cancellationTokenSource.Token));
        }

        private async Task ProcessWithPythonService(List<AudioFileItem> files, CancellationToken cancellationToken)
        {
            try
            {
                // Start Python service
                _pythonService.StartPythonService();

                // Check if service is running
                var isRunning = await _pythonService.PingPythonService();
                if (!isRunning)
                {
                    DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                    {
                        StepsContainer.Visibility = Visibility.Visible;
                        ProgressBorder.Visibility = Visibility.Collapsed;
                        CreateSummaryButton.IsEnabled = true;

                        var dialog = new ContentDialog
                        {
                            Title = "Ошибка",
                            Content = "Не удалось подключиться к Python сервису. Убедитесь, что Python скрипт запущен.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await dialog.ShowAsync();
                    });
                    return;
                }

                // Convert AudioFileItem to FileData for Python service
                var fileData = files.Select(f => new FileData
                {
                    Path = f.FilePath,
                    Character = f.CharacterName
                }).ToList();

                var vault = _appConfig.Vault;
                var subdirectory = _appConfig.SubDirectory;
                var format = _appConfig.Format;

                // Start processing
                var success = await _pythonService.StartProcessing(fileData, vault, subdirectory, format);

                if (!success)
                {
                    DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                    {
                        // Show steps back and hide progress
                        StepsContainer.Visibility = Visibility.Visible;
                        ProgressBorder.Visibility = Visibility.Collapsed;
                        CreateSummaryButton.IsEnabled = true;

                        var dialog = new ContentDialog
                        {
                            Title = "Ошибка",
                            Content = "Не удалось запустить обработку файлов.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await dialog.ShowAsync();
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Processing was cancelled
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                {
                    // Show steps back and hide progress
                    StepsContainer.Visibility = Visibility.Visible;
                    ProgressBorder.Visibility = Visibility.Collapsed;
                    CreateSummaryButton.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
                    // Show steps back and hide progress
                    StepsContainer.Visibility = Visibility.Visible;
                    ProgressBorder.Visibility = Visibility.Collapsed;
                    CreateSummaryButton.IsEnabled = true;

                    var dialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = $"Произошла ошибка: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                });
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _pythonService?.StopPythonService();
            // Show steps back and hide progress
            StepsContainer.Visibility = Visibility.Visible;
            ProgressBorder.Visibility = Visibility.Collapsed;
            CreateSummaryButton.IsEnabled = true;
        }

        public void Dispose()
        {
            _pythonService?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        [GeneratedRegex(@"\d+-(\S*)\..*")]
        private static partial Regex FilenameRegex();
    }

    public class AudioFileItem : INotifyPropertyChanged
    {
        private string? _characterName;

        public string? FileName { get; set; }
        public string? FilePath { get; set; }

        public string CharacterName
        {
            get => _characterName;
            set
            {
                _characterName = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}