using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace Archivist.Services
{
    public class DialogService : IDialogService
    {
        public async Task ShowDialogAsync(string title, string content)
        {
            var mainWindow = (Application.Current as App)?.MainWindow;
            if (mainWindow == null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = mainWindow.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}