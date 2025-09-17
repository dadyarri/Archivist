using Archivist.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;

namespace Archivist.Views
{
    /// <summary>
    /// Page for application settings.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private SettingsPageViewModel ViewModel { get; }

        public SettingsPage()
        {
            InitializeComponent();
            ViewModel = new SettingsPageViewModel(DispatcherQueue.GetForCurrentThread(), async () => { await ShowSaveNotificationAsync(); });
            DataContext = ViewModel;
        }

        private async Task ShowSaveNotificationAsync(int milliseconds = 1500)
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