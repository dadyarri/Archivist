using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Archivist.Services
{
    public class FilePickerService : IFilePickerService
    {
        public async Task<StorageFile?> PickFileAsync(string fileTypeFilter = "*")
        {
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(fileTypeFilter);
            filePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            filePicker.ViewMode = PickerViewMode.List;

            var mainWindow = (Application.Current as App)?.MainWindow;
            if (mainWindow == null)
            {
                return null;
            }

            var hwnd = WindowNative.GetWindowHandle(mainWindow);
            InitializeWithWindow.Initialize(filePicker, hwnd);

            return await filePicker.PickSingleFileAsync();
        }

        public async Task<StorageFolder?> PickFolderAsync()
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.ViewMode = PickerViewMode.List;

            var mainWindow = (Application.Current as App)?.MainWindow;
            if (mainWindow == null)
            {
                return null;
            }

            var hwnd = WindowNative.GetWindowHandle(mainWindow);
            InitializeWithWindow.Initialize(folderPicker, hwnd);

            return await folderPicker.PickSingleFolderAsync();
        }
    }
}